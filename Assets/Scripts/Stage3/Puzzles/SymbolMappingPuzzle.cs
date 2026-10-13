using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using EscapeRoom.Interaction;
using EscapeRoom.Core;
using EscapeRoom.UI;
using EscapeRoom.Player;

namespace EscapeRoom.Stage3.Puzzles
{
    /// <summary>
    /// Symbol-to-number mapping puzzle for Stage 3.
    /// Generates a random mapping of 5 symbols to unique digits each playthrough.
    /// Player must deduce the mapping from clues on monitor screens and enter a
    /// 4-digit code derived from a displayed equation.
    /// </summary>
    [DisallowMultipleComponent]
    public class SymbolMappingPuzzle : MonoBehaviour, IInteractable
    {
        private static SymbolMappingPuzzle instance;
        public static SymbolMappingPuzzle Instance => instance;

        [Header("Puzzle Configuration")]
        [Tooltip("Symbols used in the puzzle.")]
        [SerializeField] private string[] symbols = new string[] { "▲", "●", "■", "◆", "★" };

        [Tooltip("Time penalty in seconds for wrong answer.")]
        [SerializeField] private float wrongAnswerPenalty = 10f;

        [Header("State")]
        [SerializeField] private bool isCompleted = false;
        [SerializeField] private bool isUIOpen = false;

        // Runtime randomized mapping: symbol index -> digit value (1-9)
        private int[] symbolValues;

        // The target code the player must enter (4 digits derived from symbols)
        private string targetCode = "";

        // The equation displayed to the player (e.g., "▲ ● ■ ◆ = ???")
        private string equationDisplay = "";

        // Indices of the 4 symbols used in the target equation
        private int[] equationSymbolIndices;

        // Clue equations for monitor screens
        private string[] monitorClues;

        // UI input state
        private string enteredCode = "";
        private string statusMessage = "";
        private Color statusColor = Color.white;
        private float statusTimer = 0f;

        public event Action OnPuzzleCompleted;

        public bool IsCompleted => isCompleted;
        public bool IsUIOpen => isUIOpen;
        public string[] MonitorClues => monitorClues;
        public string EquationDisplay => equationDisplay;

        public string InteractionPrompt
        {
            get
            {
                if (isCompleted) return "Terminal (Solved)";
                if (MemoryPathPuzzle.Instance != null && !MemoryPathPuzzle.Instance.IsCompleted)
                    return "Symbol Terminal [LOCKED: Clear Floor Grid First]";
                return "Press E to access Symbol Terminal";
            }
        }
        public bool CanInteract => !isCompleted && !isUIOpen;

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

        private void Start()
        {
            GenerateRandomMapping();
        }

        private void Update()
        {
            if (statusTimer > 0f)
            {
                statusTimer -= Time.deltaTime;
                if (statusTimer <= 0f)
                {
                    statusMessage = "";
                }
            }

            if (isUIOpen)
            {
                HandleUIInput();
            }
        }

        /// <summary>
        /// Generates a completely random symbol-to-digit mapping and creates clues.
        /// </summary>
        private void GenerateRandomMapping()
        {
            // Assign unique random values 1-9 to each of the 5 symbols
            symbolValues = new int[symbols.Length];
            var usedValues = new System.Collections.Generic.List<int>();

            for (int i = 0; i < symbols.Length; i++)
            {
                int val;
                do
                {
                    val = UnityEngine.Random.Range(1, 10);
                } while (usedValues.Contains(val));

                symbolValues[i] = val;
                usedValues.Add(val);
            }

            // Pick 4 symbols for the target equation (use first 4 of a shuffled list)
            equationSymbolIndices = new int[4];
            var indices = new System.Collections.Generic.List<int>();
            for (int i = 0; i < symbols.Length; i++) indices.Add(i);

            // Fisher-Yates shuffle
            for (int i = indices.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                int temp = indices[i];
                indices[i] = indices[j];
                indices[j] = temp;
            }

            for (int i = 0; i < 4; i++)
            {
                equationSymbolIndices[i] = indices[i];
            }

            // Build the target code from the symbol values
            targetCode = "";
            equationDisplay = "Enter: ";
            for (int i = 0; i < 4; i++)
            {
                int symIdx = equationSymbolIndices[i];
                targetCode += symbolValues[symIdx].ToString();
                equationDisplay += symbols[symIdx];
                if (i < 3) equationDisplay += "  ";
            }

            // Generate monitor clues (equations that help deduce individual symbol values)
            GenerateClues();

            Debug.Log($"<color=#5cb85c><b>[SymbolMappingPuzzle]</b></color> Generated mapping: " +
                      $"{symbols[0]}={symbolValues[0]}, {symbols[1]}={symbolValues[1]}, " +
                      $"{symbols[2]}={symbolValues[2]}, {symbols[3]}={symbolValues[3]}, " +
                      $"{symbols[4]}={symbolValues[4]}. Target code: {targetCode}");
        }

        /// <summary>
        /// Creates logically-solvable clue equations and pushes them to wall monitors.
        /// Monitor 1: Directly reveals symbol[0] and gives symbol[0]+symbol[1] sum.
        /// Monitor 2: Reveals the difference between symbol[2] and symbol[0], making symbol[2] deducible.
        /// Monitor 3: Directly reveals symbol[3] and gives symbol[4]+symbol[3] sum.
        /// Together these 5 independent relations uniquely determine all 5 symbol values.
        /// </summary>
        private void GenerateClues()
        {
            // ── Build clue text for each monitor ──────────────────────

            // Monitor 1: Two facts that together reveal symbols[0] and symbols[1]
            string clue1Line1 = $"CALIBRATION READING:  {symbols[0]} = {symbolValues[0]}";
            string clue1Line2 = $"SENSOR SUM:  {symbols[0]} + {symbols[1]} = {symbolValues[0] + symbolValues[1]}";

            // Monitor 2: Relationship that reveals symbols[2] (since symbols[0] is already known)
            int diff = symbolValues[2] - symbolValues[0];
            string clue2Line1;
            if (diff >= 0)
                clue2Line1 = $"OFFSET LOG:  {symbols[2]} - {symbols[0]} = {diff}";
            else
                clue2Line1 = $"OFFSET LOG:  {symbols[0]} - {symbols[2]} = {-diff}";

            // Monitor 3: Directly reveals symbols[3], then symbols[4] via sum
            string clue3Line1 = $"VERIFIED ID:  {symbols[3]} = {symbolValues[3]}";
            string clue3Line2 = $"PAIR TOTAL:  {symbols[4]} + {symbols[3]} = {symbolValues[4] + symbolValues[3]}";

            // Store as array for legacy access
            monitorClues = new string[]
            {
                clue1Line1 + "\n" + clue1Line2,
                clue2Line1,
                clue3Line1 + "\n" + clue3Line2
            };

            // ── Push clues to in-world MonitorDisplayUI components ────

            PushCluesToMonitors(clue1Line1, clue1Line2, clue2Line1, clue3Line1, clue3Line2);

            // ── Feed cipher terminal hint ────────────────────────────

            if (Stage3CipherHelper.Instance != null)
            {
                Stage3CipherHelper.Instance.SetSymbolHints(GetRevealedMapping());
            }
        }

        /// <summary>
        /// Finds the three wall monitors by name and writes clue content into them.
        /// </summary>
        private void PushCluesToMonitors(
            string clue1Line1, string clue1Line2,
            string clue2Line1,
            string clue3Line1, string clue3Line2)
        {
            var allMonitors = FindObjectsByType<EscapeRoom.Stage3.UI.MonitorDisplayUI>(
                FindObjectsSortMode.None);

            foreach (var mon in allMonitors)
            {
                string goName = mon.gameObject.name;

                if (goName == "MonitorScreen_1")
                {
                    mon.MonitorTitle = "SYSTEM CALIBRATION";
                    mon.MonitorText = clue1Line1;
                    mon.SecondaryText = clue1Line2;
                }
                else if (goName == "MonitorScreen_2")
                {
                    mon.MonitorTitle = "DIAGNOSTIC LOG";
                    mon.MonitorText = clue2Line1;
                    mon.SecondaryText = "Cross-reference with calibration data.";
                }
                else if (goName == "MonitorScreen_3")
                {
                    mon.MonitorTitle = "IDENTITY VERIFICATION";
                    mon.MonitorText = clue3Line1;
                    mon.SecondaryText = clue3Line2;
                }
            }
        }

        public void Interact()
        {
            if (isCompleted || isUIOpen) return;

            // Strict sequence gating: Color -> Red Box -> Path -> Symbol Box Lock
            if (MemoryPathPuzzle.Instance != null && !MemoryPathPuzzle.Instance.IsCompleted)
            {
                FeedbackHUD.ShowMessage("TERMINAL LOCKED — Complete the floor grid path first!", Color.yellow);
                Stage3AudioManager.Instance?.PlayAlarmSound();
                return;
            }

            OpenUI();
        }

        private void OpenUI()
        {
            isUIOpen = true;
            enteredCode = "";
            statusMessage = "";

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            var look = FindAnyObjectByType<PlayerLook>();
            if (look != null) look.SetCursorLock(false);
        }

        private void CloseUI()
        {
            isUIOpen = false;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            var look = FindAnyObjectByType<PlayerLook>();
            if (look != null) look.SetCursorLock(true);
        }

        private void HandleUIInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                CloseUI();
                return;
            }

            // Number key input
            for (int i = 0; i <= 9; i++)
            {
                Key numKey = (Key)((int)Key.Digit0 + i);
                Key padKey = (Key)((int)Key.Numpad0 + i);

                if (keyboard[numKey].wasPressedThisFrame || keyboard[padKey].wasPressedThisFrame)
                {
                    if (enteredCode.Length < 4)
                    {
                        enteredCode += i.ToString();
                    }
                }
            }

            // Backspace
            if (keyboard.backspaceKey.wasPressedThisFrame && enteredCode.Length > 0)
            {
                enteredCode = enteredCode.Substring(0, enteredCode.Length - 1);
            }

            // Enter to submit
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
            {
                SubmitCode();
            }
        }

        private void SubmitCode()
        {
            if (enteredCode.Length != 4)
            {
                statusMessage = "Enter exactly 4 digits";
                statusColor = new Color(0.95f, 0.85f, 0.25f);
                statusTimer = 2f;
                return;
            }

            if (string.Equals(enteredCode, targetCode, StringComparison.Ordinal))
            {
                // Correct!
                isCompleted = true;
                statusMessage = "ACCESS GRANTED";
                statusColor = new Color(0.40f, 0.95f, 0.45f);
                statusTimer = 3f;

                Debug.Log($"<color=#5cb85c><b>[SymbolMappingPuzzle]</b></color> Solved! Code: {targetCode}");
                FeedbackHUD.ShowMessage("Symbol Terminal: ACCESS GRANTED!", new Color(0.40f, 0.95f, 0.45f));

                if (Stage3AudioManager.Instance != null)
                {
                    Stage3AudioManager.Instance.PlayCompletionFanfare();
                }

                ObjectiveManager.Instance?.CompleteFlag("SYMBOL_PUZZLE_COMPLETE");
                OnPuzzleCompleted?.Invoke();

                StartCoroutine(CloseAfterDelay(1.5f));
            }
            else
            {
                // Wrong!
                statusMessage = "ACCESS DENIED - Incorrect Code";
                statusColor = new Color(0.95f, 0.35f, 0.35f);
                statusTimer = 2f;
                enteredCode = "";

                Debug.Log($"<color=#d9534f><b>[SymbolMappingPuzzle]</b></color> Wrong code entered: {enteredCode}. Expected: {targetCode}");
                FeedbackHUD.ShowMessage("Symbol Terminal: ACCESS DENIED!", new Color(0.95f, 0.35f, 0.35f));

                if (Stage3AudioManager.Instance != null)
                {
                    Stage3AudioManager.Instance.PlayErrorSound();
                }

                if (Stage3Timer.Instance != null)
                {
                    Stage3Timer.Instance.ApplyPenalty(wrongAnswerPenalty);
                }
            }
        }

        private IEnumerator CloseAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            CloseUI();
        }

        /// <summary>
        /// Returns a string describing one revealed mapping for cipher hints.
        /// </summary>
        public string GetRevealedMapping()
        {
            if (symbolValues == null || symbolValues.Length == 0) return "";
            int idx = UnityEngine.Random.Range(0, symbols.Length);
            return $"{symbols[idx]} = {symbolValues[idx]}";
        }

        private void OnGUI()
        {
            if (!isUIOpen) return;

            Color oldColor = GUI.color;

            // Full-screen dim
            GUI.color = new Color(0.03f, 0.04f, 0.06f, 0.85f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

            float cardW = Mathf.Min(480f, Screen.width * 0.9f);
            float cardH = 420f;
            float cardX = (Screen.width - cardW) * 0.5f;
            float cardY = (Screen.height - cardH) * 0.5f;

            // Panel frame
            GUI.color = new Color(0.10f, 0.12f, 0.16f, 0.98f);
            GUI.Box(new Rect(cardX, cardY, cardW, cardH), GUIContent.none);

            // Gold border
            GUI.color = new Color(0.96f, 0.78f, 0.20f, 0.85f);
            GUI.Box(new Rect(cardX + 3, cardY + 3, cardW - 6, cardH - 6), GUIContent.none);

            // Background
            GUI.color = new Color(0.12f, 0.14f, 0.18f, 1f);
            GUI.Box(new Rect(cardX + 5, cardY + 5, cardW - 10, cardH - 10), GUIContent.none);

            GUI.color = Color.white;

            // Title
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.98f, 0.85f, 0.25f) }
            };
            GUI.Label(new Rect(cardX + 20, cardY + 18, cardW - 40, 30), "SYMBOL TERMINAL", titleStyle);

            // Equation display
            var eqStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(cardX + 20, cardY + 65, cardW - 40, 40), equationDisplay, eqStyle);

            // Instruction
            var instrStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.70f, 0.75f, 0.80f) }
            };
            GUI.Label(new Rect(cardX + 20, cardY + 110, cardW - 40, 20),
                "Decode each symbol's value and enter the 4-digit number", instrStyle);

            // Divider
            GUI.color = new Color(0.96f, 0.78f, 0.20f, 0.40f);
            GUI.DrawTexture(new Rect(cardX + 30, cardY + 140, cardW - 60, 2), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Code input display
            string displayCode = "";
            for (int i = 0; i < 4; i++)
            {
                if (i < enteredCode.Length)
                    displayCode += enteredCode[i] + "  ";
                else
                    displayCode += "_  ";
            }

            var codeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 36,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.35f, 0.95f, 0.95f) }
            };
            GUI.Label(new Rect(cardX + 20, cardY + 160, cardW - 40, 50), displayCode.Trim(), codeStyle);

            // Number pad buttons
            float btnSize = 48f;
            float padStartX = cardX + (cardW - (3 * btnSize + 20)) * 0.5f;
            float padStartY = cardY + 225f;

            var btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            GUI.backgroundColor = new Color(0.25f, 0.28f, 0.35f);

            // 1-9 grid
            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 3; col++)
                {
                    int num = row * 3 + col + 1;
                    float bx = padStartX + col * (btnSize + 10);
                    float by = padStartY + row * (btnSize + 8);

                    if (GUI.Button(new Rect(bx, by, btnSize, btnSize), num.ToString(), btnStyle))
                    {
                        if (enteredCode.Length < 4)
                            enteredCode += num.ToString();
                    }
                }
            }

            // Bottom row: Clear, 0, Submit
            float bottomY = padStartY + 3 * (btnSize + 8);

            GUI.backgroundColor = new Color(0.60f, 0.25f, 0.25f);
            if (GUI.Button(new Rect(padStartX, bottomY, btnSize, btnSize), "CLR", btnStyle))
            {
                enteredCode = "";
            }

            GUI.backgroundColor = new Color(0.25f, 0.28f, 0.35f);
            if (GUI.Button(new Rect(padStartX + btnSize + 10, bottomY, btnSize, btnSize), "0", btnStyle))
            {
                if (enteredCode.Length < 4)
                    enteredCode += "0";
            }

            GUI.backgroundColor = new Color(0.25f, 0.65f, 0.35f);
            if (GUI.Button(new Rect(padStartX + 2 * (btnSize + 10), bottomY, btnSize, btnSize), "OK", btnStyle))
            {
                SubmitCode();
            }

            // Status message
            if (!string.IsNullOrEmpty(statusMessage) && statusTimer > 0f)
            {
                var statStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = statusColor }
                };
                GUI.Label(new Rect(cardX + 20, cardY + cardH - 65, cardW - 40, 24), statusMessage, statStyle);
            }

            // Close hint
            var closeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.55f, 0.60f, 0.65f) }
            };
            GUI.Label(new Rect(cardX + 20, cardY + cardH - 35, cardW - 40, 20), "[ESC] to close", closeStyle);

            GUI.backgroundColor = Color.white;
            GUI.color = oldColor;
        }
    }
}
