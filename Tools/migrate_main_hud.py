#!/usr/bin/env python3
"""Migrate Main.unity from hand-placed HUD to prefab-based StripContainer/RackBarContainer."""

from __future__ import annotations

import re
import sys
from pathlib import Path

SCENE = Path(__file__).resolve().parents[1] / "Assets" / "Scenes" / "Main.unity"

STRIP_CONTAINER_GO = 880001001
STRIP_CONTAINER_RT = 880001002
STRIP_CONTAINER_HLG = 880001003

CANVAS_RT = 791422575
RACK_GO = 526848379
RACK_RT = 526848380
RACK_HLG = 526848381
RACK_CR = 526848383

# RectTransform fileIDs to delete with full subtrees.
DELETE_RT_ROOTS = {
    959989889,   # Order1
    1612370092,  # Order2
    1588568582,  # Slot1
    1501260017,  # Slot2
    1919868433,  # Slot3
    796979189,   # Slot4
    937001386,   # Slot5
    1521842591,  # Slot6
}

ALSO_DELETE_FILE_IDS = {RACK_HLG, RACK_CR}


def parse_blocks(lines: list[str]) -> list[tuple[int, int, str]]:
    starts = [i for i, line in enumerate(lines) if line.startswith("--- !u!")]
    blocks = []
    for idx, start in enumerate(starts):
        end = starts[idx + 1] if idx + 1 < len(starts) else len(lines)
        blocks.append((start, end, "".join(lines[start:end])))
    return blocks


def block_id(block: str) -> int | None:
    m = re.search(r"^--- !u!\d+ &(\d+)", block, re.MULTILINE)
    return int(m.group(1)) if m else None


def parse_references(block: str) -> set[int]:
    return {int(x) for x in re.findall(r"\{fileID: (\d+)\}", block)}


def collect_descendant_rts(blocks: list[tuple[int, int, str]]) -> set[int]:
    rt_children: dict[int, set[int]] = {}
    rt_to_go: dict[int, int] = {}
    go_to_components: dict[int, set[int]] = {}

    for _, _, block in blocks:
        fid = block_id(block)
        if fid is None:
            continue
        if block.startswith("--- !u!224"):
            children = set()
            m = re.search(r"  m_Children:\n((?:  - \{fileID: \d+\}\n)*)", block)
            if m:
                children = {int(x) for x in re.findall(r"\{fileID: (\d+)\}", m.group(1))}
            rt_children[fid] = children
            m_go = re.search(r"m_GameObject: \{fileID: (\d+)\}", block)
            if m_go:
                rt_to_go[fid] = int(m_go.group(1))
        elif block.startswith("--- !u!1 "):
            comps = {int(x) for x in re.findall(r"- component: \{fileID: (\d+)\}", block)}
            go_to_components[fid] = comps

    remove_rts: set[int] = set()
    stack = list(DELETE_RT_ROOTS)
    while stack:
        rt = stack.pop()
        if rt in remove_rts:
            continue
        remove_rts.add(rt)
        stack.extend(rt_children.get(rt, ()))

    remove_ids = set(ALSO_DELETE_FILE_IDS)
    for rt in remove_rts:
        remove_ids.add(rt)
        go = rt_to_go.get(rt)
        if go is not None:
            remove_ids.add(go)
            remove_ids.update(go_to_components.get(go, ()))

    # Include all blocks that reference only deleted GOs/components - scan component blocks tied to deleted GOs.
    for _, _, block in blocks:
        fid = block_id(block)
        if fid is None:
            continue
        m_go = re.search(r"m_GameObject: \{fileID: (\d+)\}", block)
        if m_go and int(m_go.group(1)) in remove_ids:
            remove_ids.add(fid)

    return remove_ids


def preserve_yaml_header(lines: list[str], block_starts: list[int]) -> str:
    """Unity scenes require %YAML 1.1 / %TAG lines before the first --- block."""
    prefix = ""
    if block_starts:
        prefix = "".join(lines[: block_starts[0]])
    if not prefix.startswith("%YAML 1.1"):
        prefix = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n"
    return prefix


def main() -> int:
    lines = SCENE.read_text(encoding="utf-8").splitlines(keepends=True)
    block_starts = [i for i, line in enumerate(lines) if line.startswith("--- !u!")]
    blocks = parse_blocks(lines)
    remove_ids = collect_descendant_rts(blocks)

    body_lines: list[str] = []
    for start, end, block in blocks:
        fid = block_id(block)
        if fid in remove_ids:
            continue
        body_lines.extend(lines[start:end])

    content = preserve_yaml_header(lines, block_starts) + "".join(body_lines)

    content = content.replace("  m_Name: Rack\n", "  m_Name: RackBarContainer\n", 1)
    content = re.sub(
        r"(m_GameObject: \{fileID: 526848379\}\n  serializedVersion: 6\n  m_Component:\n  - component: \{fileID: 526848380\})\n  - component: \{fileID: 526848383\}\n  - component: \{fileID: 526848381\}",
        r"\1",
        content,
        count=1,
    )
    content = re.sub(
        r"(m_GameObject: \{fileID: 526848379\}\n(?:.*\n)*?  m_Children:\n)(?:  - \{fileID: \d+\}\n)*",
        r"\1  []\n",
        content,
        count=1,
    )
    content = re.sub(
        r"(m_GameObject: \{fileID: 791422574\}\n(?:.*\n)*?  m_Children:\n"
        r"  - \{fileID: 1276962939\}\n"
        r"  - \{fileID: 346736263\}\n)"
        r"  - \{fileID: 526848380\}\n"
        r"  - \{fileID: 959989889\}\n"
        r"  - \{fileID: 1612370092\}\n",
        rf"\1  - {{fileID: {STRIP_CONTAINER_RT}}}\n  - {{fileID: {RACK_RT}}}\n",
        content,
        count=1,
    )
    content = re.sub(
        r"  orderStrips:\n(?:  - .*\n)+  rackSlotImages:\n(?:  - .*\n)+",
        "  orderStrips: []\n  rackSlotImages: []\n",
        content,
        count=1,
    )
    content = re.sub(
        r"  stripContainer: \{fileID: 791422575\}\n",
        f"  stripContainer: {{fileID: {STRIP_CONTAINER_RT}}}\n",
        content,
        count=1,
    )
    content = content.replace("  buildOnAwake: 0\n", "  buildOnAwake: 1\n", 1)

    strip_block = f"""--- !u!1 &{STRIP_CONTAINER_GO}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {STRIP_CONTAINER_RT}}}
  - component: {{fileID: {STRIP_CONTAINER_HLG}}}
  m_Layer: 5
  m_Name: StripContainer
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{STRIP_CONTAINER_RT}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {STRIP_CONTAINER_GO}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {CANVAS_RT}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0.5, y: 1}}
  m_AnchorMax: {{x: 0.5, y: 1}}
  m_AnchoredPosition: {{x: 0, y: -50}}
  m_SizeDelta: {{x: 996.8889, y: 150}}
  m_Pivot: {{x: 0.5, y: 1}}
--- !u!114 &{STRIP_CONTAINER_HLG}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {STRIP_CONTAINER_GO}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: 30649d3a9faa99c48a7b1166b86bf2a0, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Padding:
    m_Left: 0
    m_Right: 0
    m_Top: 0
    m_Bottom: 0
  m_ChildAlignment: 4
  m_Spacing: 40
  m_ChildForceExpandWidth: 0
  m_ChildForceExpandHeight: 0
  m_ChildControlWidth: 0
  m_ChildControlHeight: 0
  m_ChildScaleWidth: 0
  m_ChildScaleHeight: 0
  m_ReverseArrangement: 0
"""

    marker = "--- !u!1 &526848379\n"
    if marker not in content:
        print("ERROR: RackBarContainer marker not found", file=sys.stderr)
        return 1
    content = content.replace(marker, strip_block + marker, 1)

    SCENE.write_text(content, encoding="utf-8")
    print(f"Migrated {SCENE}")
    print(f"Removed {len(remove_ids)} fileIDs")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
