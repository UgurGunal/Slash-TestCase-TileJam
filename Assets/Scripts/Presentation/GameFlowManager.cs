using Core;
using Gameplay;
using UnityEngine;

namespace Presentation
{
    /// <summary>
    /// Tracks high-level outcome (playing / won / lost), keeps last stats snapshot, and toggles win vs lose UI.
    /// Wire <see cref="winPanel"/> / <see cref="losePanel"/> to your Canvas overlay objects.
    /// </summary>
    public sealed class GameFlowManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] LevelBoardLoader boardLoader;
        [Tooltip("Shown when all orders are completed.")]
        [SerializeField] GameObject winPanel;
        [Tooltip("Shown when the rack fills (level failed).")]
        [SerializeField] GameObject losePanel;

        [Header("Behaviour")]
        [Tooltip("Deactivate both panels whenever a new level session is loaded.")]
        [SerializeField] bool hidePanelsOnNewLevel = true;

        LevelObjectiveSession _session;
        bool _outcomeHandled;

        public GamePhase Phase { get; private set; } = GamePhase.Idle;
        public GameStatsSnapshot LastOutcomeStats { get; private set; }

        void OnEnable()
        {
            if (boardLoader != null)
                boardLoader.SessionAssigned += OnSessionAssigned;
        }

        void OnDisable()
        {
            if (boardLoader != null)
                boardLoader.SessionAssigned -= OnSessionAssigned;
            UnhookSession();
        }

        void Start()
        {
            if (boardLoader == null)
            {
                Debug.LogWarning("[GameFlowManager] Assign boardLoader.", this);
                return;
            }

            if (boardLoader.Session != null)
                OnSessionAssigned(boardLoader.Session);
        }

        void OnSessionAssigned(LevelObjectiveSession session)
        {
            UnhookSession();
            _session = session;
            _outcomeHandled = false;
            Phase = session != null ? GamePhase.Playing : GamePhase.Idle;
            if (_session != null)
                _session.StateChanged += OnSessionStateChanged;
            if (hidePanelsOnNewLevel)
                SetEndPanels(win: false, lose: false);
        }

        void UnhookSession()
        {
            if (_session != null)
                _session.StateChanged -= OnSessionStateChanged;
            _session = null;
        }

        void OnSessionStateChanged()
        {
            if (_outcomeHandled || _session == null)
                return;

            if (_session.HasWon)
            {
                _outcomeHandled = true;
                Phase = GamePhase.Won;
                LastOutcomeStats = GameStatsSnapshot.FromSession(_session);
                SetEndPanels(win: true, lose: false);
                return;
            }

            if (_session.HasFailed)
            {
                _outcomeHandled = true;
                Phase = GamePhase.LostRackFull;
                LastOutcomeStats = GameStatsSnapshot.FromSession(_session);
                SetEndPanels(win: false, lose: true);
            }
        }

        void SetEndPanels(bool win, bool lose)
        {
            if (winPanel != null)
                winPanel.SetActive(win);
            if (losePanel != null)
                losePanel.SetActive(lose);
        }

        /// <summary>Current progress while playing; default if no session.</summary>
        public GameStatsSnapshot GetLiveStats() => GameStatsSnapshot.FromSession(_session);
    }
}
