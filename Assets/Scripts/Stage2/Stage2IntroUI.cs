using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using EscapeRoom.Core;
using EscapeRoom.Player;
using EscapeRoom.UI;

namespace EscapeRoom.Stage2
{
    /// <summary>
    /// Narrative story intro for Stage 2: Storage Room.
    /// Displays sequential narrative story cards explaining the descent into Sector B,
    /// the automated lockdown, the missing keycard, and the mission objectives.
    /// Features Prev/Next navigation, progress indicators, and a prominent Skip button
    /// allowing players to immediately jump into active gameplay.
    /// Freezes Time.timeScale = 0 during display and unlocks cursor for interaction.
    /// </summary>
    [DisallowMultipleComponent]
    public class Stage2IntroUI : MonoBehaviour
    {
        private static Stage2IntroUI instance;
        public static Stage2IntroUI Instance => instance;

        [System.Serializable]
        public class StoryCard
        {
            public string tag;
            public string title;
            public string subtitle;
            [TextArea(4, 8)]
            public string body;
        }

        [Header("Settings")]
        [SerializeField] private bool showOnStart = true;

        [Header("Story Cards")]
        [SerializeField] private StoryCard[] cards = new StoryCard[]
        {
            new StoryCard
            {
                tag = "SUB-LEVEL DESCENT // SECTOR B",
                title = "BREACHING SECTOR B",
                subtitle = "AUTOMATED STORAGE // FACILITY TRANSIT LEVEL",
                body = "You escaped the Attic and forced your way down the emergency service shaft into Sector B: Automated Storage.\n\n" +
                       "Behind you, the heavy pressure blast doors slam shut with a deafening metallic shudder. The stairwell is permanently sealed by automated containment protocols.\n\n" +
                       "There is no going back. The only way out is forward."
            },
            new StoryCard
            {
                tag = "SECURITY PROTOCOL // HIGH ALERT",
                title = "FACILITY LOCKDOWN ENGAGED",
                subtitle = "CONTAINMENT ALERT // PASSAGE COMPROMISED",
                body = "Automated security sirens pulse in the distance. The reinforced blast door leading to the subterranean Cellar corridor is locked under high-voltage magnetic clamps.\n\n" +
                       "The wall-mounted biometric security reader requires an authorized [Master Storage Keycard] to release the door.\n\n" +
                       "Without this card, you will be trapped in the sector when total facility sterilization engages."
            },
            new StoryCard
            {
                tag = "INTEL // SUPERVISOR SHIFT LOG",
                title = "THE REINFORCED VAULT & THE LOGIC MATRIX",
                subtitle = "RESTRICTED PASSCODE // SECURITY CHIP MISSING",
                body = "The shift supervisor secured the Master Keycard inside the reinforced storage vault safe on the north wall, protected by an encrypted 3-digit combination.\n\n" +
                       "The central Pattern Recognition Terminal on the assembly workbench can decrypt the safe combination—but its logic matrix is completely offline.\n\n" +
                       "It requires a physical [Terminal Security Chip] to initialize the deciphering sequence."
            },
            new StoryCard
            {
                tag = "OPERATIONAL DIRECTIVE // RECOVERY CHECKLIST",
                title = "YOUR MISSION: RETRIEVE & ESCAPE",
                subtitle = "TACTICAL ESCAPE PLAN",
                body = "• 1. Examine the Shift Log clipboard near the door for technician storage clues.\n" +
                       "• 2. Search sector storage containers (desk, cabinet, shelf, crate, tool case) to find the Security Chip.\n" +
                       "• 3. Insert the chip into the Pattern Terminal and solve the logic sequence to reveal the safe code.\n" +
                       "• 4. Enter the 3-digit code on the vault keypad to retrieve the Master Keycard.\n" +
                       "• 5. Swipe the exit door security reader and escape into the cellar corridor before lockdown engages!"
            }
        };

        [Header("State")]
        [SerializeField] private bool isOpen = false;
        [SerializeField] private int currentCardIndex = 0;
        private static bool justClosedThisFrame = false;

        public bool IsOpen => isOpen;
        public static bool IsIntroOpen => instance != null && instance.isOpen;
        public static bool JustClosedThisFrame => justClosedThisFrame;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneLoadedListener()
        {
            SceneManager.sceneLoaded -= OnSceneLoadedGlobal;
            SceneManager.sceneLoaded += OnSceneLoadedGlobal;
        }

        private static void OnSceneLoadedGlobal(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "StorageRoom")
            {
                var existing = UnityEngine.Object.FindAnyObjectByType<Stage2IntroUI>();
                if (existing == null)
                {
                    GameObject go = new GameObject("Stage2_IntroUI");
                    existing = go.AddComponent<Stage2IntroUI>();
                    Debug.Log("<color=#00bcd4>[Stage2IntroUI]</color> Auto-instantiated Stage 2 Story Intro on sceneLoaded for StorageRoom.");
                }

                if (existing != null && !existing.isOpen)
                {
                    existing.ShowIntro();
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void EnsureIntroUI()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.name == "StorageRoom")
            {
                var existing = UnityEngine.Object.FindAnyObjectByType<Stage2IntroUI>();
                if (existing == null)
                {
                    GameObject go = new GameObject("Stage2_IntroUI");
                    existing = go.AddComponent<Stage2IntroUI>();
                }

                if (existing != null && !existing.isOpen)
                {
                    existing.ShowIntro();
                }
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

            if (cards == null || cards.Length == 0)
            {
                cards = GetDefaultCards();
            }
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
            if (showOnStart && !isOpen)
            {
                ShowIntro();
            }
        }

        private void Update()
        {
            if (!isOpen) return;

            // Keep cursor unlocked and visible while intro modal is open
            if (Cursor.lockState != CursorLockMode.None || !Cursor.visible)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                // Escape key skips intro immediately
                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    OnSkipIntro();
                    return;
                }

                // Space or Enter advances to next card (or begins if on last card)
                if (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                {
                    OnNextCard();
                    return;
                }

                // Left Arrow / A can go back
                if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame)
                {
                    OnPreviousCard();
                    return;
                }

                // Right Arrow / D can go next
                if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame)
                {
                    OnNextCard();
                    return;
                }
            }
        }

        public void ShowIntro()
        {
            isOpen = true;
            currentCardIndex = 0;
            Time.timeScale = 0f;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            var playerLook = FindAnyObjectByType<PlayerLook>();
            if (playerLook != null)
            {
                playerLook.SetCursorLock(false);
            }

            Stage2Audio.Instance?.PlayButtonPress();
            Debug.Log("<color=#00bcd4>[Stage2IntroUI]</color> Stage 2 Story Intro opened. Time.timeScale set to 0.");
        }

        public void OnNextCard()
        {
            Stage2Audio.Instance?.PlayButtonPress();
            currentCardIndex++;
            if (currentCardIndex >= cards.Length)
            {
                FinishIntro();
            }
        }

        public void OnPreviousCard()
        {
            if (currentCardIndex > 0)
            {
                Stage2Audio.Instance?.PlayButtonPress();
                currentCardIndex--;
            }
        }

        public void OnSkipIntro()
        {
            Debug.Log("<color=#00bcd4>[Stage2IntroUI]</color> Player chose to SKIP intro. Advancing directly to gameplay.");
            FinishIntro();
        }

        private void FinishIntro()
        {
            isOpen = false;
            justClosedThisFrame = true;
            Time.timeScale = 1f;

            // In case PauseManager was triggered, force resume
            if (PauseManager.Instance != null && PauseManager.Instance.IsPaused)
            {
                PauseManager.Instance.ResumeGame();
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            var playerLook = FindAnyObjectByType<PlayerLook>();
            if (playerLook != null)
            {
                playerLook.SetCursorLock(true);
            }

            // Activate objectives system for Stage 2
            ObjectiveManager.Instance?.ActivateSystem();

            // Trigger Stage 2 room banner
            BannerUI.Instance?.ShowBanner("STAGE 2: STORAGE SECTOR", "Investigate the facility shift log near the entrance.");

            Stage2Audio.Instance?.PlayCorrectAnswer();
            Debug.Log("<color=#5cb85c>[Stage2IntroUI]</color> Intro complete. Active gameplay engaged, cursor locked, objectives activated.");
        }

        private void LateUpdate()
        {
            if (justClosedThisFrame)
            {
                if (PauseManager.Instance != null && PauseManager.Instance.IsPaused)
                {
                    PauseManager.Instance.ResumeGame();
                }
                justClosedThisFrame = false;
            }
        }

        private void OnGUI()
        {
            if (!isOpen) return;
            if (cards == null || cards.Length == 0) cards = GetDefaultCards();
            if (cards == null || cards.Length == 0) return;

            GUI.depth = -90;

            int totalCards = cards.Length;
            int cardIdx = Mathf.Clamp(currentCardIndex, 0, totalCards - 1);
            StoryCard card = cards[cardIdx];
            bool isLastCard = (cardIdx == totalCards - 1);

            Color oldColor = GUI.color;

            // Full-screen dark cinematic backdrop
            GUI.color = new Color(0.04f, 0.06f, 0.09f, 0.94f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

            // Responsive Card Window
            float winW = Mathf.Min(680f, Screen.width * 0.92f);
            float winH = Mathf.Min(520f, Screen.height * 0.90f);
            float winX = (Screen.width - winW) * 0.5f;
            float winY = (Screen.height - winH) * 0.5f;

            // Chassis & Border
            GUI.color = new Color(0.10f, 0.12f, 0.17f, 0.98f);
            GUI.Box(new Rect(winX, winY, winW, winH), GUIContent.none);

            // Cyan/teal glowing border line
            GUI.color = new Color(0.15f, 0.75f, 0.85f, 0.90f);
            GUI.Box(new Rect(winX + 4, winY + 4, winW - 8, winH - 8), GUIContent.none);

            // Inner dark panel
            GUI.color = new Color(0.12f, 0.15f, 0.21f, 1f);
            GUI.Box(new Rect(winX + 8, winY + 8, winW - 16, winH - 16), GUIContent.none);

            // Top Header Bar
            GUI.color = new Color(0.16f, 0.22f, 0.30f, 1f);
            GUI.Box(new Rect(winX + 12, winY + 12, winW - 24, 62f), GUIContent.none);

            GUI.color = Color.white;

            // Card Tag (e.g. SUB-LEVEL DESCENT // SECTOR B)
            var tagStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(winX + 24, winY + 16, winW - 170, 20f), $"[ {card.tag} ]", tagStyle);

            // Card Counter Indicator (e.g. CARD 1 OF 4)
            var counterStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(winX + winW - 180, winY + 16, 156, 20f), $"CARD {cardIdx + 1} OF {totalCards}", counterStyle);

            // Card Title (20pt Bold White)
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(winX + 24, winY + 36, winW - 170, 32f), card.title, titleStyle);

            // Subtitle Line
            var subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(winX + 24, winY + 80, winW - 48, 20f), card.subtitle, subStyle);

            // Subtle divider line
            GUI.color = new Color(0.20f, 0.70f, 0.85f, 0.40f);
            GUI.DrawTexture(new Rect(winX + 24, winY + 104, winW - 48, 2), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Body Narrative Text
            float bodyY = winY + 118f;
            float bodyH = winH - 200f;
            var bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                wordWrap = true,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(winX + 28, bodyY, winW - 56, bodyH), card.body, bodyStyle);

            // Page Dots Indicator (e.g. ● ○ ○ ○)
            float footerY = winY + winH - 66f;
            string dots = "";
            for (int i = 0; i < totalCards; i++)
            {
                dots += (i == cardIdx) ? "● " : "○ ";
            }
            var dotsStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(winX + 24, footerY - 22f, winW - 48, 20f), dots.TrimEnd(), dotsStyle);

            // Bottom Navigation Bar
            // 1. PREVIOUS Button
            float navBtnH = 42f;
            float prevBtnW = 110f;
            var navBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            GUI.enabled = (cardIdx > 0);
            if (GUI.Button(new Rect(winX + 24, footerY, prevBtnW, navBtnH), "◄ PREV", navBtnStyle))
            {
                OnPreviousCard();
            }
            GUI.enabled = true;

            // 2. SKIP INTRO Button (Always available on EVERY card!)
            float skipBtnW = 160f;
            float skipBtnX = winX + (winW - skipBtnW) * 0.5f;

            var skipBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            GUI.backgroundColor = new Color(0.20f, 0.22f, 0.28f, 1f);
            if (GUI.Button(new Rect(skipBtnX, footerY, skipBtnW, navBtnH), "SKIP INTRO [ESC]", skipBtnStyle))
            {
                OnSkipIntro();
            }
            GUI.backgroundColor = Color.white;

            // 3. NEXT / BEGIN Button
            float nextBtnW = isLastCard ? 170f : 120f;
            float nextBtnX = winX + winW - nextBtnW - 24f;

            var actionBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            if (isLastCard)
            {
                GUI.backgroundColor = new Color(0.35f, 0.92f, 0.50f, 1f);
                if (GUI.Button(new Rect(nextBtnX, footerY, nextBtnW, navBtnH), "START STAGE 2 ►", actionBtnStyle))
                {
                    FinishIntro();
                }
                GUI.backgroundColor = Color.white;
            }
            else
            {
                GUI.backgroundColor = new Color(0.15f, 0.55f, 0.85f, 1f);
                if (GUI.Button(new Rect(nextBtnX, footerY, nextBtnW, navBtnH), "NEXT ►", actionBtnStyle))
                {
                    OnNextCard();
                }
                GUI.backgroundColor = Color.white;
            }

            GUI.color = oldColor;
        }

        public static StoryCard[] GetDefaultCards()
        {
            return new StoryCard[]
            {
                new StoryCard
                {
                    tag = "SUB-LEVEL DESCENT // SECTOR B",
                    title = "BREACHING SECTOR B",
                    subtitle = "AUTOMATED STORAGE // FACILITY TRANSIT LEVEL",
                    body = "You escaped the Attic and forced your way down the emergency service shaft into Sector B: Automated Storage.\n\n" +
                           "Behind you, the heavy pressure blast doors slam shut with a deafening metallic shudder. The stairwell is permanently sealed by automated containment protocols.\n\n" +
                           "There is no going back. The only way out is forward."
                },
                new StoryCard
                {
                    tag = "SECURITY PROTOCOL // HIGH ALERT",
                    title = "FACILITY LOCKDOWN ENGAGED",
                    subtitle = "CONTAINMENT ALERT // PASSAGE COMPROMISED",
                    body = "Automated security sirens pulse in the distance. The reinforced blast door leading to the subterranean Cellar corridor is locked under high-voltage magnetic clamps.\n\n" +
                           "The wall-mounted biometric security reader requires an authorized [Master Storage Keycard] to release the door.\n\n" +
                           "Without this card, you will be trapped in the sector when total facility sterilization engages."
                },
                new StoryCard
                {
                    tag = "INTEL // SUPERVISOR SHIFT LOG",
                    title = "THE REINFORCED VAULT & THE LOGIC MATRIX",
                    subtitle = "RESTRICTED PASSCODE // SECURITY CHIP MISSING",
                    body = "The shift supervisor secured the Master Keycard inside the reinforced storage vault safe on the north wall, protected by an encrypted 3-digit combination.\n\n" +
                           "The central Pattern Recognition Terminal on the assembly workbench can decrypt the safe combination—but its logic matrix is completely offline.\n\n" +
                           "It requires a physical [Terminal Security Chip] to initialize the deciphering sequence."
                },
                new StoryCard
                {
                    tag = "OPERATIONAL DIRECTIVE // RECOVERY CHECKLIST",
                    title = "YOUR MISSION: RETRIEVE & ESCAPE",
                    subtitle = "TACTICAL ESCAPE PLAN",
                    body = "• 1. Examine the Shift Log clipboard near the door for technician storage clues.\n" +
                           "• 2. Search sector storage containers (desk, cabinet, shelf, crate, tool case) to find the Security Chip.\n" +
                           "• 3. Insert the chip into the Pattern Terminal and solve the logic sequence to reveal the safe code.\n" +
                           "• 4. Enter the 3-digit code on the vault keypad to retrieve the Master Keycard.\n" +
                           "• 5. Swipe the exit door security reader and escape into the cellar corridor before lockdown engages!"
                }
            };
        }
    }
}
