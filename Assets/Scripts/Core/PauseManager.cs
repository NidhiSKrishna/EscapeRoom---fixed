using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using EscapeRoom.UI;
using EscapeRoom.Interaction;
using EscapeRoom.Player;

namespace EscapeRoom.Core
{
    /// <summary>
    /// Manages game pause state and prioritizes Escape key input across all stages (Level 1 - 4).
    /// Auto-ensures PauseManager and PauseUI across all gameplay scenes.
    /// Priority order:
    /// 1. If Keypad modal is open -> Close Keypad
    /// 2. If Clue modal is open -> Close Clue
    /// 3. If Stage-specific puzzle modal is open -> Close modal
    /// 4. If Briefing/Intro modal is open -> Let modal handle Escape
    /// 5. If Expanded Objective view is open -> Close it
    /// 6. If Game completed -> Do not pause
    /// 7. Otherwise -> Toggle Pause/Resume
    /// </summary>
    [DisallowMultipleComponent]
    public class PauseManager : MonoBehaviour
    {
        private static PauseManager instance;
        public static PauseManager Instance => instance;

        [Header("State")]
        [SerializeField] private bool isPaused = false;

        public event Action<bool> OnPauseStateChanged;

        public bool IsPaused => isPaused;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneLoadedHandler()
        {
            SceneManager.sceneLoaded -= OnSceneLoadedGlobal;
            SceneManager.sceneLoaded += OnSceneLoadedGlobal;
        }

        private static void OnSceneLoadedGlobal(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "MainMenu") return;

            var pm = UnityEngine.Object.FindAnyObjectByType<PauseManager>();
            if (pm == null)
            {
                var go = new GameObject("PauseManager");
                pm = go.AddComponent<PauseManager>();
                Debug.Log($"<color=#5cb85c>[PauseManager]</color> Auto-instantiated PauseManager for {scene.name}.");
            }

            var pui = UnityEngine.Object.FindAnyObjectByType<PauseUI>();
            if (pui == null)
            {
                var targetGo = (pm != null) ? pm.gameObject : new GameObject("PauseUI");
                pui = targetGo.AddComponent<PauseUI>();
                Debug.Log($"<color=#5cb85c>[PauseManager]</color> Auto-instantiated PauseUI for {scene.name}.");
            }
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }
            instance = this;

            // Ensure PauseUI is present on this GameObject or in the scene
            if (PauseUI.Instance == null)
            {
                var pui = FindAnyObjectByType<PauseUI>();
                if (pui == null)
                {
                    gameObject.AddComponent<PauseUI>();
                }
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                HandleEscapeKeyPress();
            }
        }

        /// <summary>
        /// Evaluates priority when Escape is pressed.
        /// </summary>
        public void HandleEscapeKeyPress()
        {
            // Priority 1: Keypad modal is open -> let Keypad close, do not pause
            if (KeypadUI.Instance != null && KeypadUI.Instance.IsOpen)
            {
                return;
            }

            // Priority 2: Clue modal is open -> let Clue close, do not pause
            if (ClueInteractable.IsAnyClueOpen)
            {
                return;
            }

            // Priority 2b: Stage 2 Modals are open -> close modal, do not pause
            if (EscapeRoom.Stage2.Stage2StorageContainer.IsKeypadOpen)
            {
                EscapeRoom.Stage2.Stage2StorageContainer.Instance?.CloseKeypad();
                return;
            }

            if (EscapeRoom.Stage2.Stage2PatternTerminal.IsTerminalOpen)
            {
                EscapeRoom.Stage2.Stage2PatternTerminal.Instance?.CloseTerminal();
                return;
            }

            if (EscapeRoom.Stage2.Stage2CipherAdapter.IsDocumentOpen)
            {
                EscapeRoom.Stage2.Stage2CipherAdapter.Instance?.CloseDocument();
                return;
            }

            // Priority 2c: Stage 3 Modals
            if (EscapeRoom.Stage3.Puzzles.SymbolMappingPuzzle.Instance != null && EscapeRoom.Stage3.Puzzles.SymbolMappingPuzzle.Instance.IsUIOpen)
            {
                return;
            }

            if (EscapeRoom.Stage3.UI.MonitorDisplayUI.IsAnyMonitorOpen)
            {
                return;
            }

            // Priority 2d: Stage 4 Modals
            if (Stage4TestPlayer.InputLocked)
            {
                return;
            }

            // Priority 3: Briefing or Intro is open -> do not pause or bypass with Escape
            if (AtticBriefingUI.IsBriefingOpen || PhoneIntroUI.IsIntroOpen || EscapeRoom.Stage2.Stage2IntroUI.IsIntroOpen || EscapeRoom.Stage2.Stage2IntroUI.JustClosedThisFrame)
            {
                return;
            }

            // Priority 4: Expanded Objective view is open -> close it, do not pause
            if (ObjectiveHUD.IsExpandedViewOpen && ObjectiveHUD.Instance != null)
            {
                ObjectiveHUD.Instance.CloseExpandedView();
                return;
            }

            // Priority 5: Game already completed (escaped) -> do not pause
            if (GameManager.Instance != null && GameManager.Instance.HasEscaped)
            {
                return;
            }

            // Priority 6: Toggle pause state
            TogglePause();
        }

        public void TogglePause()
        {
            if (isPaused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }

        public void PauseGame()
        {
            if (isPaused) return;
            isPaused = true;
            Time.timeScale = 0f;

            // Unlock mouse cursor for menu navigation
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            var playerLook = FindAnyObjectByType<PlayerLook>();
            if (playerLook != null)
            {
                playerLook.SetCursorLock(false);
            }

            if (PauseUI.Instance != null)
            {
                PauseUI.Instance.ShowPauseMenu();
            }
            else
            {
                var pui = gameObject.AddComponent<PauseUI>();
                pui.ShowPauseMenu();
            }

            Debug.Log("[PauseManager] Game paused.");
            OnPauseStateChanged?.Invoke(true);
        }

        public void ResumeGame()
        {
            if (!isPaused) return;
            isPaused = false;
            Time.timeScale = 1f;

            if (PauseUI.Instance != null)
            {
                PauseUI.Instance.HidePauseMenu();
            }

            // Lock cursor back to gameplay
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            var playerLook = FindAnyObjectByType<PlayerLook>();
            if (playerLook != null)
            {
                playerLook.SetCursorLock(true);
            }

            Debug.Log("[PauseManager] Game resumed.");
            OnPauseStateChanged?.Invoke(false);
        }

        public void RestartGame()
        {
            ResumeGame();
            if (GameManager.Instance != null)
            {
                GameManager.Instance.RestartGame();
            }
            else
            {
                Scene currentScene = SceneManager.GetActiveScene();
                SceneManager.LoadScene(currentScene.buildIndex);
            }
        }
    }
}
