using UnityEngine;

namespace Presentation
{
    /// <summary>
    /// Makes the OS cursor visible and unlocked in Play Mode so screen captures show it.
    /// Applies automatically for every scene (Main, Level Editor, etc.). Does not affect Edit Mode in the Unity Editor.
    /// </summary>
    public static class CursorRecordingBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void BeforeFirstSceneLoad() => SetRecordingFriendlyCursor();

        public static void SetRecordingFriendlyCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    /// <summary>
    /// Optional: add to any scene if something else hides the cursor during play; set <see cref="enforceWhilePlaying"/> to keep forcing visibility.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed class CursorRecordingHelper : MonoBehaviour
    {
        [Tooltip("Re-apply Cursor.visible each frame (use if another script locks or hides the cursor).")]
        [SerializeField] bool enforceWhilePlaying = true;

        void Awake() => CursorRecordingBootstrap.SetRecordingFriendlyCursor();

        void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
                CursorRecordingBootstrap.SetRecordingFriendlyCursor();
        }

        void LateUpdate()
        {
            if (enforceWhilePlaying)
                CursorRecordingBootstrap.SetRecordingFriendlyCursor();
        }
    }
}
