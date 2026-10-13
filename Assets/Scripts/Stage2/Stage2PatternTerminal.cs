using System;
using UnityEngine;
using UnityEngine.InputSystem;
using EscapeRoom.Interaction;
using EscapeRoom.Player;
using EscapeRoom.UI;

namespace EscapeRoom.Stage2
{
    /// <summary>
    /// Interactive computer terminal hosting the Stage 2 Pattern Recognition Puzzle.
    /// Interacting with the terminal opens an on-screen console displaying the logic sequence,
    /// multiple choice answers, and upon correct resolution reveals the locked container combination
    /// and facility search log.
    /// </summary>
    [DisallowMultipleComponent]
    public class Stage2PatternTerminal : MonoBehaviour, IInteractable
    {
        private static Stage2PatternTerminal instance;
        public static Stage2PatternTerminal Instance => instance;

        [Header("Terminal Configuration")]
        [SerializeField] private string promptText = "Press E to access Pattern Terminal";
        [SerializeField] private bool canInteract = true;
        [SerializeField] private bool requireSecurityChip = true;

        [Header("State")]
        private bool isOpen = false;
        private int openedFrame = -1;
        private string feedbackMessage = "";
        private Color feedbackColor = Color.white;
        private float feedbackTimer = 0f;

        public static bool IsTerminalOpen => instance != null && instance.isOpen;

        public bool HasSecurityChip =>
            !requireSecurityChip ||
            Stage2SearchSpot.HasCollectedTerminalChip ||
            (Stage2PuzzleGenerator.Instance != null && Stage2PuzzleGenerator.Instance.IsChipCollected);

        public string InteractionPrompt
        {
            get
            {
                if (Stage2PuzzleGenerator.Instance != null && Stage2PuzzleGenerator.Instance.IsPatternSolved)
                {
                    return "Pattern Terminal (Decrypted)";
                }

                if (!HasSecurityChip)
                {
                    return "Pattern Terminal [LOCKED: Requires Security Chip]";
                }

                return "Press E to access Pattern Terminal";
            }
        }

        public string PromptText => InteractionPrompt;
        public bool CanInteract => canInteract && !isOpen;
        public bool RequireSecurityChip { get => requireSecurityChip; set => requireSecurityChip = value; }

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

        public void Interact()
        {
            if (!HasSecurityChip)
            {
                Stage2Audio.Instance?.PlayWrongAnswer();
                FeedbackHUD.ShowMessage("ACCESS DENIED: Pattern Terminal is locked. Find the Security Access Chip first! (Check Shift Log for clues)", new Color(0.95f, 0.35f, 0.35f));
                return;
            }

            OpenTerminal();
        }

        public void OpenTerminal()
        {
            isOpen = true;
            openedFrame = Time.frameCount;
            feedbackMessage = "";
            Stage2Audio.Instance?.PlayButtonPress();

            var look = FindAnyObjectByType<PlayerLook>();
            if (look != null)
            {
                look.SetCursorLock(false);
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void CloseTerminal()
        {
            if (!isOpen) return;
            isOpen = false;
            Stage2Audio.Instance?.PlayButtonPress();

            var look = FindAnyObjectByType<PlayerLook>();
            if (look != null)
            {
                look.SetCursorLock(true);
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void Update()
        {
            if (!isOpen || Time.frameCount == openedFrame) return;

            // Ensure cursor remains unlocked and visible while terminal is open
            if (Cursor.lockState != CursorLockMode.None || !Cursor.visible)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (feedbackTimer > 0f)
            {
                feedbackTimer -= Time.deltaTime;
                if (feedbackTimer <= 0f)
                {
                    feedbackMessage = "";
                }
            }

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame)
                {
                    CloseTerminal();
                    return;
                }

                // Keyboard shortcuts for pattern selection [1 - 4]
                var puzzleGen = Stage2PuzzleGenerator.Instance;
                if (puzzleGen != null && !puzzleGen.IsPatternSolved && HasSecurityChip && puzzleGen.ActivePattern != null)
                {
                    var options = puzzleGen.ActivePattern.answerOptions;
                    if (options != null)
                    {
                        for (int i = 0; i < Mathf.Min(4, options.Length); i++)
                        {
                            Key digitKey = Key.Digit1 + i;
                            Key numpadKey = Key.Numpad1 + i;
                            if (keyboard[digitKey].wasPressedThisFrame || keyboard[numpadKey].wasPressedThisFrame)
                            {
                                SelectOption(i);
                                return;
                            }
                        }
                    }
                }
            }
        }

        public void SelectOption(int index)
        {
            var puzzleGen = Stage2PuzzleGenerator.Instance;
            if (puzzleGen == null || puzzleGen.ActivePattern == null) return;
            var pattern = puzzleGen.ActivePattern;
            if (index < 0 || index >= pattern.answerOptions.Length) return;

            string opt = pattern.answerOptions[index];
            Stage2Audio.Instance?.PlayPatternSelect();
            if (puzzleGen.ValidatePatternAnswer(opt))
            {
                Stage2Audio.Instance?.PlayCorrectAnswer();
                feedbackMessage = "LOGIC SEQUENCE VERIFIED! ENCRYPTION OVERRIDDEN.";
                feedbackColor = new Color(0.35f, 0.90f, 0.40f);
                feedbackTimer = 5f;
            }
            else
            {
                Stage2Audio.Instance?.PlayWrongAnswer();
                feedbackMessage = "INCORRECT SEQUENCE ELEMENT. RE-EVALUATE THE PATTERN RULE.";
                feedbackColor = new Color(0.95f, 0.35f, 0.35f);
                feedbackTimer = 3.5f;
            }
        }

        private void OnGUI()
        {
            if (!isOpen) return;

            var puzzleGen = Stage2PuzzleGenerator.Instance;
            if (puzzleGen == null || puzzleGen.ActivePattern == null) return;

            var pattern = puzzleGen.ActivePattern;
            bool isSolved = puzzleGen.IsPatternSolved;

            // Terminal Modal Window
            float winW = Mathf.Min(780f, Screen.width * 0.90f);
            float winH = Mathf.Min(620f, Screen.height * 0.90f);
            float winX = (Screen.width - winW) * 0.5f;
            float winY = (Screen.height - winH) * 0.5f;

            Color oldColor = GUI.color;

            // Background terminal chassis
            GUI.color = new Color(0.06f, 0.08f, 0.12f, 0.96f);
            GUI.Box(new Rect(winX, winY, winW, winH), GUIContent.none);

            // Inner CRT green/cyan border line
            GUI.color = new Color(0.20f, 0.60f, 0.70f, 0.85f);
            GUI.Box(new Rect(winX + 4, winY + 4, winW - 8, winH - 8), GUIContent.none);

            // Header banner
            GUI.color = new Color(0.10f, 0.14f, 0.20f, 1f);
            GUI.Box(new Rect(winX + 6, winY + 6, winW - 12, 54f), GUIContent.none);

            // Title Style (White #FFFFFF)
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            GUI.color = Color.white;
            GUI.Label(new Rect(winX + 10, winY + 10, winW - 20, 24f), "PATTERN TERMINAL // SECURITY LOGIC CONSOLE", titleStyle);

            var subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.70f, 0.75f, 0.80f) }
            };
            GUI.Label(new Rect(winX + 10, winY + 34, winW - 20, 20f), $"SUBROUTINE: {pattern.puzzleTitle.ToUpper()} [{pattern.category.ToUpper()}]", subStyle);

            float curY = winY + 70f;

            if (!HasSecurityChip)
            {
                // Terminal Locked: Security Module Missing
                GUI.color = new Color(0.20f, 0.08f, 0.08f, 0.95f);
                GUI.Box(new Rect(winX + 24, curY, winW - 48, 220f), GUIContent.none);

                var lockTitle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 17,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.95f, 0.40f, 0.40f) }
                };
                GUI.color = Color.white;
                GUI.Label(new Rect(winX + 30, curY + 25, winW - 60, 30f), "🔒 ACCESS DENIED: SECURITY MODULE NOT DETECTED", lockTitle);

                var lockDesc = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    wordWrap = true,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white }
                };
                GUI.Label(new Rect(winX + 40, curY + 70, winW - 80, 75f), 
                    "The pattern deciphering subroutine is completely offline.\n" +
                    "A physical [Terminal Security Chip] must be inserted to activate the console.\n\n" +
                    "Search the storage room to locate and retrieve the chip.\n" +
                    "(Review the Shift Log clipboard near the entrance for its specific storage location.)", lockDesc);

                curY += 235f;
            }
            else
            {
                // Has Security Chip!
                // Section 1: Sequence Display Panel
                GUI.color = new Color(0.04f, 0.06f, 0.09f, 1f);
                GUI.Box(new Rect(winX + 24, curY, winW - 48, 96f), GUIContent.none);

                var seqStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 17,
                    fontStyle = FontStyle.Bold,
                    wordWrap = true,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white }
                };

                GUI.color = Color.white;
                GUI.Label(new Rect(winX + 30, curY + 8, winW - 60, 48f), pattern.sequenceDisplay, seqStyle);

                var hintStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.65f, 0.70f, 0.75f) }
                };
                GUI.Label(new Rect(winX + 30, curY + 62, winW - 60, 24f), "Identify the logical pattern progression to determine the final missing element.", hintStyle);

                curY += 108f;

                if (!isSolved)
            {
                // Unsolved: Show 4 Answer Option Buttons
                var promptLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = Color.white }
                };
                GUI.Label(new Rect(winX + 30, curY, winW - 60, 24f), "SELECT THE NEXT LOGICAL VALUE IN THE SEQUENCE (Click or Press 1-4):", promptLabelStyle);
                curY += 28f;

                float btnW = (winW - 70f) * 0.5f;
                float btnH = 50f;

                var btnStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 15,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.white }
                };

                for (int i = 0; i < pattern.answerOptions.Length; i++)
                {
                    int row = i / 2;
                    int col = i % 2;
                    float bx = winX + 24f + col * (btnW + 22f);
                    float by = curY + row * (btnH + 12f);

                    string opt = pattern.answerOptions[i];
                    string btnLabel = $"[{i + 1}]  {opt}";
                    if (GUI.Button(new Rect(bx, by, btnW, btnH), btnLabel, btnStyle))
                    {
                        SelectOption(i);
                    }
                }

                curY += 125f;

                // Feedback message
                if (!string.IsNullOrEmpty(feedbackMessage))
                {
                    var msgStyle = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 13,
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleCenter,
                        normal = { textColor = feedbackColor }
                    };
                    GUI.Label(new Rect(winX + 24, curY, winW - 48, 26f), feedbackMessage, msgStyle);
                }
            }
            else
            {
                // Solved State: Display Combination & Facility Search Log
                GUI.color = new Color(0.12f, 0.28f, 0.16f, 0.90f);
                GUI.Box(new Rect(winX + 24, curY, winW - 48, 220f), GUIContent.none);

                var successTitle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 16,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.40f, 0.95f, 0.45f) }
                };
                GUI.color = Color.white;
                GUI.Label(new Rect(winX + 30, curY + 12, winW - 60, 26f), "✔ SEQUENCE RECOGNITION VALIDATED", successTitle);

                var explanationStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.85f, 0.90f, 0.85f) }
                };
                GUI.Label(new Rect(winX + 30, curY + 38, winW - 60, 20f), $"Rule: {pattern.ruleExplanation}", explanationStyle);

                // Big Combination Box
                float codeBoxW = 340f;
                float codeBoxH = 50f;
                float codeBoxX = winX + (winW - codeBoxW) * 0.5f;
                float codeBoxY = curY + 65f;

                GUI.color = new Color(0.05f, 0.08f, 0.06f, 0.95f);
                GUI.Box(new Rect(codeBoxX, codeBoxY, codeBoxW, codeBoxH), GUIContent.none);

                var codeStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 20,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.98f, 0.92f, 0.35f) }
                };
                GUI.color = Color.white;
                GUI.Label(new Rect(codeBoxX, codeBoxY, codeBoxW, codeBoxH), $"CONTAINER CODE: [ {puzzleGen.ContainerCombination} ]", codeStyle);

                // Clue note for search location
                var clueHeaderStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white }
                };
                GUI.Label(new Rect(winX + 30, curY + 126f, winW - 60, 20f), "RECOVERED FACILITY MEMO:", clueHeaderStyle);

                var clueBodyStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    wordWrap = true,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.80f, 0.85f, 0.90f) }
                };
                GUI.Label(new Rect(winX + 40, curY + 150f, winW - 80, 55f), puzzleGen.ActiveSearchSpot.clueNote, clueBodyStyle);

                curY += 230f;
            }
            }

            // Close Button at Bottom
            float closeW = 180f;
            float closeH = 38f;
            float closeX = winX + (winW - closeW) * 0.5f;
            float closeY = winY + winH - 52f;

            var closeBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            if (GUI.Button(new Rect(closeX, closeY, closeW, closeH), "CLOSE [ESC / E]", closeBtnStyle))
            {
                CloseTerminal();
            }

            GUI.color = oldColor;
        }
    }
}
