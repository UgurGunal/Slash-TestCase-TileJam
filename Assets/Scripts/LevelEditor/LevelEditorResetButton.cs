using UnityEngine;
using UnityEngine.UI;

namespace Presentation
{
    /// <summary>
    /// Calls <see cref="LevelEditorTilePlacementController.ResetLevelEditor"/> — clears orders, board, rack, hand, and related UI.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelEditorResetButton : MonoBehaviour
    {
        [SerializeField] LevelEditorTilePlacementController placementController;
        [SerializeField] Button resetButton;

        void OnEnable()
        {
            if (resetButton != null)
                resetButton.onClick.AddListener(OnResetClicked);
        }

        void OnDisable()
        {
            if (resetButton != null)
                resetButton.onClick.RemoveListener(OnResetClicked);
        }

        void OnResetClicked()
        {
            if (placementController == null)
            {
                Debug.LogWarning("[LevelEditorResetButton] Assign placement controller.");
                return;
            }

            placementController.ResetLevelEditor();
        }
    }
}
