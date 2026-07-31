using Unity.CodeEditor;
using UnityEditor;
using UnityEngine;

namespace LevelEditor
{
    /// <summary>
    /// Generates Unity .sln / .csproj so Cursor/VS Code C# Go to Definition works.
    /// Offline fallback (when Editor is busy): Tools/GenerateCursorSolution.ps1
    /// </summary>
    public static class RegenerateIdeProjectFilesMenu
    {
        [MenuItem("TileJam/Regenerate C# Project Files for Cursor")]
        public static void Regenerate()
        {
            CodeEditor.Editor.CurrentCodeEditor.SyncAll();
            Debug.Log(
                "[TileJam] Regenerated IDE project files (.sln / .csproj). " +
                "Reload the Cursor window (Ctrl+Shift+P → Reload Window), then try F12.");
        }
    }
}
