using Gameplay;
using UnityEngine;

namespace Presentation
{
    /// <summary>
    /// Tracks high-level outcome (playing / won / lost) and toggles win vs lose UI.
    /// Subscribes to <see cref="IGameplayEventBus"/> for victory and rack-full outcomes.
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
        IGameplayEventBus _eventBus;
        bool _outcomeHandled;

        public LevelBoardLoader BoardLoader => boardLoader;

        void OnEnable()
        {
            if (boardLoader != null)
                boardLoader.SessionAssigned += OnSessionAssigned;
        }

        void OnDisable()
        {
            if (boardLoader != null)
                boardLoader.SessionAssigned -= OnSessionAssigned;
            Unhook();
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
            Unhook();
            _session = session;
            _eventBus = boardLoader != null ? boardLoader.GameplayEventBus : null;
            _outcomeHandled = false;

            if (_eventBus != null)
            {
                _eventBus.Subscribe<VictoryEvent>(OnVictory);
                _eventBus.Subscribe<RackFullEvent>(OnRackFull);
            }

            if (hidePanelsOnNewLevel)
                SetEndPanels(win: false, lose: false);
        }

        void Unhook()
        {
            if (_eventBus != null)
            {
                _eventBus.Unsubscribe<VictoryEvent>(OnVictory);
                _eventBus.Unsubscribe<RackFullEvent>(OnRackFull);
            }

            _eventBus = null;
            _session = null;
        }

        void OnVictory(VictoryEvent _)
        {
            if (_outcomeHandled || _session == null) return;
            _outcomeHandled = true;
            SetEndPanels(win: true, lose: false);
        }

        void OnRackFull(RackFullEvent _)
        {
            if (_outcomeHandled || _session == null) return;
            _outcomeHandled = true;
            SetEndPanels(win: false, lose: true);
        }

        void SetEndPanels(bool win, bool lose)
        {
            if (winPanel != null)
                winPanel.SetActive(win);
            if (losePanel != null)
                losePanel.SetActive(lose);
        }

        public void RetryCurrentLevel()
        {
            if (boardLoader == null)
            {
                Debug.LogWarning("[GameFlowManager] RetryCurrentLevel: assign boardLoader.", this);
                return;
            }

            SetEndPanels(win: false, lose: false);
            boardLoader.Reload();
        }

        public void LoadNextLevel()
        {
            if (boardLoader == null)
            {
                Debug.LogWarning("[GameFlowManager] LoadNextLevel: assign boardLoader.", this);
                return;
            }

            if (boardLoader.TryLoadNextLevel())
                SetEndPanels(win: false, lose: false);
        }
    }
}
