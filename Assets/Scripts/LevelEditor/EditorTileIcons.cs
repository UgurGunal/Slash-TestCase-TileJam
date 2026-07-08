#if UNITY_EDITOR
using System.Collections.Generic;
using Core;
using Presentation;
using UnityEditor;
using UnityEngine;

namespace LevelEditor
{
    /// <summary>Resolves <see cref="TileIconLibrary"/> sprites and draws them in IMGUI.</summary>
    static class EditorTileIcons
    {
        const string DefaultLibraryPath = "Assets/GameData/TileIconLibrary.asset";

        static TileIconLibrary _library;
        static readonly Dictionary<TileKind, Sprite> _spriteCache = new Dictionary<TileKind, Sprite>();

        public static TileIconLibrary Library
        {
            get
            {
                if (_library != null) return _library;

                _library = AssetDatabase.LoadAssetAtPath<TileIconLibrary>(DefaultLibraryPath);
                if (_library != null) return _library;

                var guids = AssetDatabase.FindAssets("t:TileIconLibrary");
                if (guids.Length > 0)
                    _library = AssetDatabase.LoadAssetAtPath<TileIconLibrary>(AssetDatabase.GUIDToAssetPath(guids[0]));
                return _library;
            }
        }

        public static int ResolvedKindCount
        {
            get
            {
                var lib = Library;
                return lib != null ? lib.ResolvedDefinitionCount : GameConstants.PlayableTileKindCount;
            }
        }

        public static bool TryGetSprite(TileKind kind, out Sprite sprite)
        {
            if (_spriteCache.TryGetValue(kind, out sprite) && sprite != null)
                return true;

            sprite = null;
            var lib = Library;
            if (lib == null || !lib.TryGetSprite(kind, out sprite) || sprite == null)
                return false;

            _spriteCache[kind] = sprite;
            return true;
        }

        static readonly Color DefaultTileBackground = new Color(0.24f, 0.25f, 0.30f, 1f);
        static readonly Color DefaultTileOutline = new Color(0f, 0f, 0f, 0.6f);

        public static void DrawTile(Rect rect, TileKind kind, Color? tint = null, Color? background = null, Color? outline = null, float outlineThickness = 1.5f)
        {
            var bg = tint ?? Color.white;
            EditorGUI.DrawRect(rect, background ?? DefaultTileBackground);

            if (!TryGetSprite(kind, out var sprite, out var tex, out var uv))
            {
                GUI.Label(rect, ((int)kind).ToString(), EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                var prev = GUI.color;
                GUI.color = bg;
                GUI.DrawTextureWithTexCoords(rect, tex, uv, true);
                GUI.color = prev;
            }

            DrawOutline(rect, outline ?? DefaultTileOutline, outlineThickness);
        }

        static bool TryGetSprite(TileKind kind, out Sprite sprite, out Texture tex, out Rect uv)
        {
            tex = null;
            uv = default;
            if (!TryGetSprite(kind, out sprite) || sprite == null)
                return false;
            tex = sprite.texture;
            if (tex == null)
                return false;
            var sr = sprite.rect;
            uv = new Rect(sr.x / tex.width, sr.y / tex.height, sr.width / tex.width, sr.height / tex.height);
            return true;
        }

        static void DrawOutline(Rect rect, Color color, float thickness)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        public static bool TileButton(Rect rect, TileKind kind, bool selected = false, Color? tint = null)
        {
            var prev = GUI.backgroundColor;
            if (selected)
                GUI.backgroundColor = new Color(0.45f, 0.75f, 1f, 1f);

            var clicked = GUI.Button(rect, GUIContent.none);
            var inner = new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f);
            DrawTile(inner, kind, tint);
            GUI.backgroundColor = prev;
            return clicked;
        }

        public static void DrawEmptySlot(Rect rect, Color color)
        {
            EditorGUI.DrawRect(rect, color);
            var border = new Color(0.25f, 0.25f, 0.28f, 1f);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), border);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), border);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), border);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), border);
        }
    }
}
#endif
