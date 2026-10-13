using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using EscapeRoom.Core;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace EscapeRoom.UI
{
    /// <summary>
    /// Handles Main Menu UI with 'CONTINUE', 'RESTART', and 'QUIT' buttons.
    /// Supports persistent progress resuming: if the player completed Stage 1,
    /// 'CONTINUE' picks up directly at Stage 2 (Storage Room).
    /// 'RESTART' resets saved progress and restarts from the beginning (Stage 1).
    /// Fully responsive to mouse clicks (GUI.Button + direct MouseDown) and keyboard hotkeys.
    /// Hotkeys: [Enter]/[Space]/[C] = Continue, [R] = Restart Game, [Q]/[Esc] = Quit.
    /// </summary>
    [DisallowMultipleComponent]
    public class MainMenuUI : MonoBehaviour
    {
        private static MainMenuUI instance;
        public static MainMenuUI Instance => instance;

        [Header("References")]
        [SerializeField] private PhoneIntroUI phoneIntroUI;

        [Header("Menu State")]
        [SerializeField] private bool isMenuVisible = true;

        public bool IsMenuVisible => isMenuVisible;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        private void Start()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (phoneIntroUI == null)
            {
                phoneIntroUI = FindAnyObjectByType<PhoneIntroUI>();
            }

            if (phoneIntroUI == null)
            {
                var go = new GameObject("PhoneIntroUI");
                phoneIntroUI = go.AddComponent<PhoneIntroUI>();
            }
        }

        private void Update()
        {
            if (!isMenuVisible) return;

            // Ensure cursor remains visible in main menu
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            bool continuePressed = false;
            bool restartPressed = false;
            bool quitPressed = false;

            // 1. New Input System Keyboard Check
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame ||
                    keyboard.spaceKey.wasPressedThisFrame || keyboard.cKey.wasPressedThisFrame)
                {
                    continuePressed = true;
                }
                else if (keyboard.rKey.wasPressedThisFrame)
                {
                    restartPressed = true;
                }
                else if (keyboard.qKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame)
                {
                    quitPressed = true;
                }
            }


            if (continuePressed)
            {
                OnContinueClicked();
            }
            else if (restartPressed)
            {
                OnRestartClicked();
            }
            else if (quitPressed)
            {
                OnQuitClicked();
            }
        }

        /// <summary>
        /// Resumes gameplay at the furthest unlocked stage.
        /// If Stage 1, launches Phone Intro -> Attic.
        /// If Stage 2+, loads the respective scene directly.
        /// </summary>
        public void OnContinueClicked()
        {
            int stage = GameProgress.GetHighestUnlockedStage();
            Debug.Log($"<color=#5cb85c><b>[MainMenuUI]</b></color> CONTINUE triggered. Resuming at Stage {stage} ({GameProgress.GetStageDisplayName(stage)})...");
            isMenuVisible = false;

            if (stage == 1)
            {
                if (phoneIntroUI != null)
                {
                    phoneIntroUI.OpenIntro();
                }
                else
                {
                    SceneManager.LoadScene(GameProgress.GetStageSceneName(1));
                }
            }
            else
            {
                string sceneName = GameProgress.GetStageSceneName(stage);
                if (Application.CanStreamedLevelBeLoaded(sceneName))
                {
                    SceneManager.LoadScene(sceneName);
                }
                else
                {
                    bool loaded = false;
                    for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
                    {
                        string path = SceneUtility.GetScenePathByBuildIndex(i);
                        if (path.IndexOf("StorageRoom", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            SceneManager.LoadScene(i);
                            loaded = true;
                            break;
                        }
                    }
                    if (!loaded)
                    {
                        SceneManager.LoadScene("StorageRoom");
                    }
                }
            }
        }

        /// <summary>
        /// Resets all saved progress and starts the entire game fresh from Stage 1.
        /// </summary>
        public void OnRestartClicked()
        {
            Debug.Log("<color=#f0ad4e><b>[MainMenuUI]</b></color> RESTART GAME triggered. Resetting all game progress to Stage 1...");
            GameProgress.ResetProgress();
            isMenuVisible = false;

            if (phoneIntroUI != null)
            {
                phoneIntroUI.OpenIntro();
            }
            else
            {
                SceneManager.LoadScene(GameProgress.GetStageSceneName(1));
            }
        }

        public void OnQuitClicked()
        {
            Debug.Log("[MainMenuUI] QUIT requested.");
#if UNITY_EDITOR
            Debug.Log("<color=#f0ad4e>[MainMenuUI]</color> Quit requested in Editor Play Mode.");
#else
            Application.Quit();
#endif
        }

        private void OnGUI()
        {
            if (!isMenuVisible) return;

            // Highest priority depth
            GUI.depth = -100;

            Color oldColor = GUI.color;

            // Full screen moody dark atmospheric background
            GUI.color = new Color(0.04f, 0.05f, 0.07f, 1f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

            // Centered menu panel
            float panelWidth = Mathf.Min(540f, Screen.width * 0.92f);
            float panelHeight = 440f;
            float panelX = (Screen.width - panelWidth) * 0.5f;
            float panelY = (Screen.height - panelHeight) * 0.5f;

            // Card frame
            GUI.color = new Color(0.10f, 0.12f, 0.16f, 0.98f);
            GUI.Box(new Rect(panelX, panelY, panelWidth, panelHeight), GUIContent.none);

            // Border accent
            GUI.color = new Color(0.96f, 0.78f, 0.20f, 0.90f);
            GUI.Box(new Rect(panelX + 3, panelY + 3, panelWidth - 6, panelHeight - 6), GUIContent.none);

            // Inner fill
            GUI.color = new Color(0.12f, 0.14f, 0.18f, 1f);
            GUI.Box(new Rect(panelX + 5, panelY + 5, panelWidth - 10, panelHeight - 10), GUIContent.none);

            GUI.color = Color.white;

            // Subtitle: WELCOME TO
            var subTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.75f, 0.80f, 0.88f) }
            };
            GUI.Label(new Rect(panelX + 20, panelY + 26, panelWidth - 40, 22), "WELCOME TO", subTitleStyle);

            // Main Title: ESCAPE ROOM
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 32,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.98f, 0.85f, 0.35f) }
            };
            GUI.Label(new Rect(panelX + 20, panelY + 50, panelWidth - 40, 44), "ESCAPE ROOM", titleStyle);

            // Divider line
            GUI.color = new Color(0.96f, 0.78f, 0.20f, 0.40f);
            GUI.DrawTexture(new Rect(panelX + 50, panelY + 102, panelWidth - 100, 2), Texture2D.whiteTexture);
            GUI.color = Color.white;

            int currentStage = GameProgress.GetHighestUnlockedStage();

            // Progress status badge
            var progressBadgeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = currentStage > 1 ? new Color(0.40f, 0.95f, 0.60f) : new Color(0.75f, 0.85f, 0.95f) }
            };
            string progressText = currentStage > 1
                ? $"SAVED PROGRESS: {GameProgress.GetStageDisplayName(currentStage).ToUpper()}"
                : "START YOUR ESCAPE ADVENTURE";
            GUI.Label(new Rect(panelX + 20, panelY + 112, panelWidth - 40, 22), progressText, progressBadgeStyle);

            // Buttons layout
            float btnW = 300f;
            float btnH = 48f;
            float btnX = panelX + (panelWidth - btnW) * 0.5f;
            float curY = panelY + 148f;

            Rect continueRect = new Rect(btnX, curY, btnW, btnH);
            curY += 62f;
            Rect restartRect = new Rect(btnX, curY, btnW, btnH);
            curY += 62f;
            Rect quitRect = new Rect(btnX, curY, btnW, 42f);

            // 1. CONTINUE Button
            string continueLabel = currentStage > 1
                ? $"CONTINUE [STAGE {currentStage}] [ENTER / C]"
                : "CONTINUE [ENTER / C]";

            var continueStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
                hover = { textColor = new Color(1f, 1f, 0.75f) },
                active = { textColor = Color.white }
            };

            // Vibrant Emerald green when continuing saved stage, Radiant Gold when fresh game
            GUI.backgroundColor = currentStage > 1
                ? new Color(0.20f, 0.85f, 0.45f, 1f)
                : new Color(0.98f, 0.82f, 0.25f, 1f);

            if (GUI.Button(continueRect, continueLabel, continueStyle))
            {
                OnContinueClicked();
            }

            // 2. RESTART Button
            var restartStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            GUI.backgroundColor = new Color(0.85f, 0.40f, 0.25f, 1f); // Warm amber-red
            if (GUI.Button(restartRect, "RESTART GAME [R]", restartStyle))
            {
                OnRestartClicked();
            }

            // 3. QUIT Button
            var quitStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.85f, 0.88f, 0.92f) }
            };

            GUI.backgroundColor = new Color(0.24f, 0.26f, 0.32f, 1f);
            if (GUI.Button(quitRect, "QUIT [ESC / Q]", quitStyle))
            {
                OnQuitClicked();
            }

            // 4. DIRECT MOUSE CLICK FALLBACK
            Event e = Event.current;
            if (e != null && e.type == EventType.MouseDown && e.button == 0)
            {
                if (continueRect.Contains(e.mousePosition))
                {
                    OnContinueClicked();
                    e.Use();
                }
                else if (restartRect.Contains(e.mousePosition))
                {
                    OnRestartClicked();
                    e.Use();
                }
                else if (quitRect.Contains(e.mousePosition))
                {
                    OnQuitClicked();
                    e.Use();
                }
            }

            GUI.backgroundColor = Color.white;
            GUI.color = oldColor;
        }
    }
}
