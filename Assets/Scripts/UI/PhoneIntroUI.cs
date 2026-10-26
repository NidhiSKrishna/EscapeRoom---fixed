using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

namespace EscapeRoom.UI
{
    /// <summary>
    /// Displays a phone-like central narrative intro modal before the Attic Briefing.
    /// Supports multi-message narrative progression, Continue/Next buttons, and a Skip Intro button.
    /// Advancing always proceeds to AtticBriefingUI (never directly into active gameplay).
    /// </summary>
    [DisallowMultipleComponent]
    public class PhoneIntroUI : MonoBehaviour
    {
        private static PhoneIntroUI instance;
        public static PhoneIntroUI Instance => instance;

        [Header("Story Messages")]
        [TextArea(3, 6)]
        [SerializeField] private string[] storyMessages = new string[]
        {
            "INCOMING ENCRYPTED MESSAGE...\n\n" +
            "\"Listen carefully. You've uncovered something you were never supposed to see. " +
            "Automated lockdown protocols have engaged throughout the facility.\"",

            "\"A security sweeps team has been dispatched. You have limited time before they sterilize the building. " +
            "You are currently trapped on the top floor: the Attic.\"",

            "\"Your only chance is to make your way down through the storage level to the Cellar and find the service exit. " +
            "Search every surface, follow the clues, and keep moving!\""
        };

        [Header("State")]
        [SerializeField] private bool isOpen = false;
        [SerializeField] private int currentMessageIndex = 0;

        public bool IsOpen => isOpen;
        public static bool IsIntroOpen => instance != null && instance.isOpen;

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

        private void Update()
        {
            if (!isOpen) return;

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    OnSkipIntroClicked();
                    return;
                }
                if (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                {
                    OnNextClicked();
                    return;
                }
            }

        }

        public void OpenIntro()
        {
            isOpen = true;
            currentMessageIndex = 0;
            Time.timeScale = 0f;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            Debug.Log("[PhoneIntroUI] Phone intro opened.");
        }

        public void CloseIntro()
        {
            isOpen = false;
        }

        public void OnNextClicked()
        {
            currentMessageIndex++;
            if (currentMessageIndex >= storyMessages.Length)
            {
                AdvanceToAtticBriefing();
            }
        }

        public void OnSkipIntroClicked()
        {
            Debug.Log("[PhoneIntroUI] Skip Intro clicked. Advancing directly to Attic Briefing.");
            AdvanceToAtticBriefing();
        }

        /// <summary>
        /// Advances to Attic Briefing card.
        /// If in MainMenu scene, loads EscapeRoom_Main (where AtticBriefingUI opens).
        /// If already in EscapeRoom_Main, directly opens AtticBriefingUI.
        /// </summary>
        public void AdvanceToAtticBriefing()
        {
            isOpen = false;

            string activeSceneName = SceneManager.GetActiveScene().name;
            if (activeSceneName == "MainMenu")
            {
                Time.timeScale = 1f;
                Debug.Log("[PhoneIntroUI] Transitioning from MainMenu to EscapeRoom_Main...");
                SceneManager.LoadScene("EscapeRoom_Main");
            }
            else
            {
                if (AtticBriefingUI.Instance != null)
                {
                    AtticBriefingUI.Instance.ShowBriefing();
                }
                else
                {
                    var briefing = FindAnyObjectByType<AtticBriefingUI>();
                    if (briefing != null)
                    {
                        briefing.ShowBriefing();
                    }
                    else
                    {
                        Time.timeScale = 1f;
                    }
                }
            }
        }

        private void OnGUI()
        {
            if (!isOpen) return;

            GUI.depth = -100;

            Color oldColor = GUI.color;

            // Full screen dim background
            GUI.color = new Color(0.02f, 0.03f, 0.05f, 0.94f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

            // Skip Intro Button in top right corner
            float skipW = 120f;
            float skipH = 36f;
            float skipX = Screen.width - skipW - 30f;
            float skipY = 30f;

            var skipStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            GUI.color = Color.white;
            GUI.backgroundColor = new Color(0.20f, 0.22f, 0.28f, 0.95f);
            if (GUI.Button(new Rect(skipX, skipY, skipW, skipH), "Skip Intro ▶▶", skipStyle))
            {
                OnSkipIntroClicked();
                GUI.color = oldColor;
                return;
            }

            // Central Phone Frame
            float phoneW = Mathf.Min(380f, Screen.width * 0.9f);
            float phoneH = Mathf.Min(640f, Screen.height * 0.92f);
            float phoneX = (Screen.width - phoneW) * 0.5f;
            float phoneY = (Screen.height - phoneH) * 0.5f;

            // Phone Outer Bezel (Dark metallic)
            GUI.color = new Color(0.12f, 0.13f, 0.15f, 0.98f);
            GUI.Box(new Rect(phoneX, phoneY, phoneW, phoneH), GUIContent.none);

            // Phone Metallic Edge Border
            GUI.color = new Color(0.40f, 0.44f, 0.50f, 0.80f);
            GUI.Box(new Rect(phoneX + 3, phoneY + 3, phoneW - 6, phoneH - 6), GUIContent.none);

            // Phone Inner Screen (OLED black)
            GUI.color = new Color(0.05f, 0.06f, 0.08f, 1f);
            GUI.Box(new Rect(phoneX + 8, phoneY + 8, phoneW - 16, phoneH - 16), GUIContent.none);

            // Speaker notch / camera bar at top of screen
            GUI.color = new Color(0.15f, 0.16f, 0.18f, 1f);
            GUI.Box(new Rect(phoneX + (phoneW - 90f) * 0.5f, phoneY + 14, 90f, 8f), GUIContent.none);

            // Status bar time & signal
            GUI.color = Color.white;
            var statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(phoneX + 15, phoneY + 26, phoneW - 30, 20), "02:47 AM  •  ENCRYPTED CELLULAR", statusStyle);

            // Message Bubble Card
            float bubbleX = phoneX + 24;
            float bubbleY = phoneY + 65;
            float bubbleW = phoneW - 48;
            float bubbleH = phoneH - 170;

            GUI.color = new Color(0.12f, 0.16f, 0.22f, 0.95f);
            GUI.Box(new Rect(bubbleX, bubbleY, bubbleW, bubbleH), GUIContent.none);

            // Bubble border
            GUI.color = new Color(0.25f, 0.40f, 0.60f, 0.60f);
            GUI.Box(new Rect(bubbleX + 2, bubbleY + 2, bubbleW - 4, bubbleH - 4), GUIContent.none);

            // Text inside message bubble
            GUI.color = Color.white;
            var textStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                wordWrap = true,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = Color.white }
            };

            string currentText = (storyMessages != null && currentMessageIndex < storyMessages.Length)
                ? storyMessages[currentMessageIndex]
                : "...";

            GUI.Label(new Rect(bubbleX + 16, bubbleY + 18, bubbleW - 32, bubbleH - 36), currentText, textStyle);

            // Page Indicator (e.g. 1 / 3)
            int totalSlides = storyMessages != null ? storyMessages.Length : 1;
            var pageStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(phoneX + 20, phoneY + phoneH - 96, phoneW - 40, 20),
                $"Message {currentMessageIndex + 1} of {totalSlides}", pageStyle);

            // Continue / Next Button
            float btnW = phoneW - 60f;
            float btnH = 42f;
            float btnX = phoneX + 30f;
            float btnY = phoneY + phoneH - btnH - 24f;
            Rect nextBtnRect = new Rect(btnX, btnY, btnW, btnH);

            string btnLabel = (currentMessageIndex >= totalSlides - 1) ? "PROCEED TO ATTIC [ENTER] ▶" : "NEXT [ENTER / SPACE] ▶";
            var nextStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            GUI.backgroundColor = new Color(0.18f, 0.28f, 0.45f, 1f);
            if (GUI.Button(nextBtnRect, btnLabel, nextStyle))
            {
                OnNextClicked();
            }

            Event e = Event.current;
            if (e != null && e.type == EventType.MouseDown && e.button == 0)
            {
                if (nextBtnRect.Contains(e.mousePosition))
                {
                    OnNextClicked();
                    e.Use();
                }
            }

            GUI.backgroundColor = Color.white;
            GUI.color = oldColor;
        }
    }
}
