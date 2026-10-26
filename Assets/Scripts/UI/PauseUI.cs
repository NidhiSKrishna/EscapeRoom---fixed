using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using EscapeRoom.Core;

namespace EscapeRoom.UI
{
    /// <summary>
    /// Screen-space Pause Menu UI rendering RESUME, RESTART, and MAIN MENU options.
    /// Operates during Time.timeScale = 0f.
    /// Fully responsive to mouse clicks (GUI.Button + direct MouseDown) and keyboard hotkeys.
    /// Hotkeys: [Esc]/[Space] = Resume, [R] = Restart, [M] = Main Menu.
    /// </summary>
    [DisallowMultipleComponent]
    public class PauseUI : MonoBehaviour
    {
        private static PauseUI instance;
        public static PauseUI Instance => instance;

        [Header("State")]
        [SerializeField] private bool isOpen = false;

        public bool IsOpen => isOpen;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
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

        private void Update()
        {
            if (!isOpen) return;

            // Ensure cursor is visible while paused
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            bool restartPressed = false;
            bool menuPressed = false;
            bool resumePressed = false;
            bool quitPressed = false;

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.rKey.wasPressedThisFrame)
                {
                    restartPressed = true;
                }
                else if (kb.mKey.wasPressedThisFrame)
                {
                    menuPressed = true;
                }
                else if (kb.spaceKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)
                {
                    resumePressed = true;
                }
                else if (kb.qKey.wasPressedThisFrame)
                {
                    quitPressed = true;
                }
            }

            if (resumePressed)
            {
                OnResumeClicked();
            }
            else if (restartPressed)
            {
                OnRestartClicked();
            }
            else if (menuPressed)
            {
                OnMainMenuClicked();
            }
            else if (quitPressed)
            {
                OnQuitClicked();
            }
        }

        public void ShowPauseMenu()
        {
            isOpen = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void HidePauseMenu()
        {
            isOpen = false;
        }

        public void OnResumeClicked()
        {
            if (PauseManager.Instance != null)
            {
                PauseManager.Instance.ResumeGame();
            }
            else
            {
                Time.timeScale = 1f;
                HidePauseMenu();
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                var look = FindAnyObjectByType<EscapeRoom.Player.PlayerLook>();
                if (look != null) look.SetCursorLock(true);
            }
        }

        public void OnRestartClicked()
        {
            if (PauseManager.Instance != null)
            {
                PauseManager.Instance.RestartGame();
            }
            else if (GameManager.Instance != null)
            {
                GameManager.Instance.RestartGame();
            }
            else
            {
                Time.timeScale = 1f;
                Scene currentScene = SceneManager.GetActiveScene();
                SceneManager.LoadScene(currentScene.buildIndex);
            }
        }

        public void OnMainMenuClicked()
        {
            if (PauseManager.Instance != null)
            {
                PauseManager.Instance.ResumeGame();
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.ReturnToMainMenu();
            }
            else
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene("MainMenu");
            }
        }

        public void OnQuitClicked()
        {
            Debug.Log("[PauseUI] QUIT GAME requested.");
            Time.timeScale = 1f;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnGUI()
        {
            if (!isOpen) return;

            GUI.depth = -100;

            // Translucent dark background overlay
            Color oldColor = GUI.color;
            GUI.color = new Color(0.04f, 0.05f, 0.08f, 0.75f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

            float panelWidth = 340f;
            float panelHeight = 345f;
            float panelX = (Screen.width - panelWidth) * 0.5f;
            float panelY = (Screen.height - panelHeight) * 0.5f;

            // Frame background
            GUI.color = new Color(0.12f, 0.14f, 0.18f, 0.96f);
            GUI.Box(new Rect(panelX, panelY, panelWidth, panelHeight), GUIContent.none);

            // Border accent
            GUI.color = new Color(0.35f, 0.55f, 0.85f, 0.85f);
            GUI.Box(new Rect(panelX + 4, panelY + 4, panelWidth - 8, panelHeight - 8), GUIContent.none);

            // Inner background
            GUI.color = new Color(0.15f, 0.17f, 0.22f, 0.98f);
            GUI.Box(new Rect(panelX + 6, panelY + 6, panelWidth - 12, panelHeight - 12), GUIContent.none);

            // Header title
            GUI.color = Color.white;
            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(panelX, panelY + 20f, panelWidth, 34), "PAUSED", titleStyle);

            // Buttons
            float btnW = 230f;
            float btnH = 40f;
            float btnX = panelX + (panelWidth - btnW) * 0.5f;
            float startY = panelY + 66f;
            float spacing = 50f;

            Rect resumeRect  = new Rect(btnX, startY, btnW, btnH);
            Rect restartRect = new Rect(btnX, startY + spacing, btnW, btnH);
            Rect menuRect    = new Rect(btnX, startY + spacing * 2f, btnW, btnH);
            Rect quitRect    = new Rect(btnX, startY + spacing * 3f, btnW, btnH);

            GUIStyle btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            GUI.color = Color.white;

            // RESUME
            GUI.backgroundColor = new Color(0.20f, 0.65f, 0.35f);
            if (GUI.Button(resumeRect, "RESUME [ESC]", btnStyle))
            {
                OnResumeClicked();
            }

            // RESTART
            GUI.backgroundColor = new Color(0.35f, 0.40f, 0.50f);
            if (GUI.Button(restartRect, "RESTART [R]", btnStyle))
            {
                OnRestartClicked();
            }

            // MAIN MENU
            GUI.backgroundColor = new Color(0.30f, 0.32f, 0.38f);
            if (GUI.Button(menuRect, "MAIN MENU [M]", btnStyle))
            {
                OnMainMenuClicked();
            }

            // QUIT GAME
            GUI.backgroundColor = new Color(0.70f, 0.20f, 0.20f);
            if (GUI.Button(quitRect, "QUIT GAME [Q]", btnStyle))
            {
                OnQuitClicked();
            }

            // Direct Mouse Click Fallback
            Event e = Event.current;
            if (e != null && e.type == EventType.MouseDown && e.button == 0)
            {
                if (resumeRect.Contains(e.mousePosition))
                {
                    OnResumeClicked();
                    e.Use();
                }
                else if (restartRect.Contains(e.mousePosition))
                {
                    OnRestartClicked();
                    e.Use();
                }
                else if (menuRect.Contains(e.mousePosition))
                {
                    OnMainMenuClicked();
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
