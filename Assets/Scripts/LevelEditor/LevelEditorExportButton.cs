using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Presentation
{
    /// <summary>
    /// UI hook for <see cref="LevelEditorTilePlacementController.TryExportLevelJson"/>. In the Unity Editor, opens a save dialog;
    /// in builds, writes under <see cref="Application.persistentDataPath"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelEditorExportButton : MonoBehaviour
    {
        [SerializeField] LevelEditorTilePlacementController placementController;
        [SerializeField] Button exportButton;

        void OnEnable()
        {
            if (exportButton != null)
                exportButton.onClick.AddListener(OnExportClicked);
        }

        void OnDisable()
        {
            if (exportButton != null)
                exportButton.onClick.RemoveListener(OnExportClicked);
        }

        void OnExportClicked()
        {
            if (placementController == null)
            {
                Debug.LogWarning("[LevelEditorExportButton] Assign placement controller.");
                return;
            }

            if (!placementController.TryExportLevelJson(out var json, out var error))
            {
                Debug.LogWarning("[LevelEditorExport] " + error);
#if UNITY_EDITOR
                EditorUtility.DisplayDialog("Export failed", error, "OK");
#endif
                return;
            }

#if UNITY_EDITOR
            var path = EditorUtility.SaveFilePanel("Export level JSON", Application.dataPath, "level", "json");
            if (string.IsNullOrEmpty(path))
                return;
            File.WriteAllText(path, json);
            AssetDatabase.Refresh();
            EditorUtility.RevealInFinder(path);
            Debug.Log($"[LevelEditorExport] Saved to {path}");
#else
            var outPath = Path.Combine(Application.persistentDataPath, $"level_export_{DateTime.Now:yyyyMMdd_HHmmss}.json");
            File.WriteAllText(outPath, json);
            Debug.Log($"[LevelEditorExport] Saved to {outPath}");
#endif
        }
    }
}
