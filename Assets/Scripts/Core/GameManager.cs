using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using EscapeRoom.UI;

namespace EscapeRoom.Core
{
    /// <summary>
    /// Lightweight game-flow manager tracking active gameplay and escape state.
    /// Exposes CompleteEscape, RestartGame, and game state events without bloating into a god object.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameManager : MonoBehaviour
    {
        private static GameManager instance;
        public static GameManager Instance => instance;

        [Header("Game State")]
        [SerializeField] private bool isGameplayActive = true;
        [SerializeField] private bool hasEscaped = false;

        public event Action OnEscapeCompleted;
        public event Action<bool> OnGameplayStateChanged;

        public bool IsGameplayActive => isGameplayActive;
        public bool HasEscaped => hasEscaped;

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            hasEscaped = false;
            isGameplayActive = true;
            Time.timeScale = 1f;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }
            instance = this;

            hasEscaped = false;
            isGameplayActive = true;
            Time.timeScale = 1f;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        /// <summary>
        /// Triggers the escape sequence when the player exits the room.
        /// Guarded to ensure it only activates once.
        /// </summary>
        public void CompleteEscape()
        {
            if (hasEscaped) return;

            hasEscaped = true;
            isGameplayActive = false;

            Debug.Log("<color=#5cb85c><b>[GameManager]</b></color> Escape Completed! Player reached the exit.");
            FeedbackHUD.ShowMessage("ESCAPE SUCCESSFUL! You solved the escape room.", new Color(0.35f, 0.95f, 0.95f));

            // Unlock Stage 2 progress
            GameProgress.SetStageUnlocked(2);

            OnEscapeCompleted?.Invoke();
            OnGameplayStateChanged?.Invoke(false);

            // Display Escape Win UI
            if (EscapeUI.Instance != null)
            {
                EscapeUI.Instance.ShowEscapeScreen();
            }
        }

        /// <summary>
        /// Reloads the active gameplay scene, resetting all puzzle and player states.
        /// </summary>
        public void RestartGame()
        {
            Debug.Log("[GameManager] Restarting game session...");
            Time.timeScale = 1f;
            Scene currentScene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(currentScene.buildIndex);
        }

        /// <summary>
        /// Placeholder for future main menu return.
        /// </summary>
        public void ReturnToMainMenu()
        {
            Debug.Log("[GameManager] Return to Main Menu requested.");
            Time.timeScale = 1f;

            // If a MainMenu scene exists in build settings, load it; otherwise reload current scene as placeholder
            if (SceneManager.sceneCountInBuildSettings > 1)
            {
                // Look for a scene named MainMenu
                for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
                {
                    string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
                    if (scenePath.IndexOf("MainMenu", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        SceneManager.LoadScene(i);
                        return;
                    }
                }
            }

            FeedbackHUD.ShowMessage("Main Menu placeholder: Reloading room...", Color.yellow);
            RestartGame();
        }
    }
}
