using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EscapeRoom.Core;
using EscapeRoom.Interaction;

namespace EscapeRoom.UI
{
    /// <summary>
    /// CIPHER — the interactive digital guide character for the Escape Room.
    /// 
    /// Cipher is a friendly, mysterious AI that guides the player through Stage 1.
    /// Implemented as a compact IMGUI portrait panel — lightweight, no 3D model required.
    /// 
    /// Features:
    ///   • Contextual dialogue on objective changes
    ///   • Three-level progressive hint system
    ///   • Inactivity detection (offers help after ~60 seconds stuck)
    ///   • Timer warning messages (5min, 2min, 1min, 30sec)
    ///   • Interactive [ASK CIPHER] button
    ///   • Connects to ObjectiveManager for current-step awareness
    ///   • Non-horror tone: confident, playful, helpful
    /// </summary>
    [DisallowMultipleComponent]
    public class CipherHostUI : MonoBehaviour
    {
        private static CipherHostUI instance;
        public static CipherHostUI Instance => instance;

        // ── Config ────────────────────────────────────────────────────────────
        [Header("Cipher Settings")]
        [SerializeField] private string hostName = "CIPHER";
        [SerializeField] private float  messageDisplayDuration = 5.5f;
        [SerializeField] private float  inactivityTimeout      = 60f;
        [SerializeField] private float  hintOfferCooldown      = 90f;

        [Header("Portrait Colors")]
        [SerializeField] private Color accentColor   = new Color(0.25f, 0.90f, 1.0f);
        [SerializeField] private Color portraitColor = new Color(0.12f, 0.22f, 0.32f);

        private Texture2D portraitTexture;

        private void EnsurePortraitLoaded()
        {
            if (portraitTexture != null) return;
            try
            {
                string path = System.IO.Path.Combine(Application.dataPath, "Art/Sprites/CipherPortrait.jpg");
                if (System.IO.File.Exists(path))
                {
                    byte[] bytes = System.IO.File.ReadAllBytes(path);
                    portraitTexture = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                    portraitTexture.LoadImage(bytes);
                    portraitTexture.filterMode = FilterMode.Bilinear;
                    portraitTexture.anisoLevel = 8;
                    portraitTexture.wrapMode = TextureWrapMode.Clamp;
                    portraitTexture.Apply(true, true);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[CipherHostUI] Could not load portrait texture: " + ex.Message);
            }
        }

        // ── State ─────────────────────────────────────────────────────────────
        private bool  isDialogueOpen   = false;
        private bool  isHintOfferOpen  = false;
        private bool  isActive         = false;  // true once gameplay starts
        private float dialogueTimer    = 0f;
        private float inactivityTimer  = 0f;
        private float hintCooldownTimer = 0f;
        private float panelAlpha       = 0f;
        private string currentMessage  = "";
        private int   lastObjectiveStep = -1;
        private int   currentHintLevel  = 0;   // 0 = none offered yet for this objective

        // ── GUI Styles ────────────────────────────────────────────────────────
        private GUIStyle nameStyle, msgStyle, btnStyle, smallBtnStyle;
        private bool stylesBuilt = false;

        // ── Hint Database ─────────────────────────────────────────────────────
        // Keyed by completion flag of the CURRENT objective step.
        private static readonly Dictionary<string, string[]> HintsByFlag =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            // ── Stage 1: Attic / Main Room ────────────────────────────────────
            ["LOCKBOX_KEY_ACQUIRED"] = new[]
            {
                "\"Ciphers don't open boxes. Keys do.\"",
                "\"Check the places where someone would normally leave a small key.\"",
                "\"Look near the desk. The key is close.\""
            },
            ["LOCKBOX_OPENED"] = new[]
            {
                "\"That lock isn't going to open itself.\"",
                "\"You have the key. Use it on the lockbox.\"",
                "\"Approach the lockbox, press E to unlock, then open it.\""
            },
            ["EXIT_NOTE_READ"] = new[]
            {
                "\"There's something inside that box. Look carefully.\"",
                "\"Take a closer look at what's inside the lockbox.\"",
                "\"There's a note inside with information you need. Read it.\""
            },
            ["EXIT_CODE_ACCEPTED"] = new[]
            {
                "\"That note had numbers on it for a reason.\"",
                "\"Find the keypad terminal and enter what the note said.\"",
                "\"Go to the keypad and enter the code from the note. It's 4 digits.\""
            },

            // ── Stage 2: Storage Room ─────────────────────────────────────────
            ["CLUE_FOUND"] = new[]
            {
                "\"Look closely around the work tables and storage shelves for documents.\"",
                "\"There is a symbol clue note left somewhere on a surface.\"",
                "\"Inspect the clue paper on the desk to discover the dial symbols.\""
            },
            ["COMBO_DISCOVERED"] = new[]
            {
                "\"The clue note diagrams the symbols required by the locker dials.\"",
                "\"Remember the symbol order shown on the parchment.\"",
                "\"Approach the locked metal cabinet to dial the correct sequence.\""
            },
            ["CABINET_UNLOCKED"] = new[]
            {
                "\"The cabinet lock tumblers have clicked open.\"",
                "\"Open the cabinet doors and look inside the compartment.\"",
                "\"Retrieve the high-security keycard stored on the shelf.\""
            },
            ["ACCESS_CARD_ACQUIRED"] = new[]
            {
                "\"You have the security access card in your inventory.\"",
                "\"Look for the wall reader next to the heavy reinforced door.\"",
                "\"Interact with the card reader to disarm the magnetic lock and escape.\""
            },
            ["CORRIDOR_UNLOCKED"] = new[]
            {
                "\"The corridor door is unsealed. Proceed onward to the next sector.\""
            },

            // ── Stage 3: Security Control Room ────────────────────────────────
            ["STAGE3_COLOR_MEMORY_SOLVED"] = new[]
            {
                "\"Step 1 is the primary color terminal on the wall.\"",
                "\"Watch the colored buttons illuminate in sequence.\"",
                "\"Repeat the exact sequence of colors to trigger the override.\""
            },
            ["STAGE3_LOCKDOWN_RESOLVED"] = new[]
            {
                "\"Step 2: Emergency lockdown! Locate the red control box immediately!\"",
                "\"Follow the flashing red sirens across the room.\"",
                "\"Press E at the red emergency panel to shut down the alarm before overload!\""
            },
            ["STAGE3_PATH_SOLVED"] = new[]
            {
                "\"Step 3: Security floor grid. Watch the floor tiles carefully.\"",
                "\"The illuminated tiles show the safe path across the grid.\"",
                "\"Step or click through the tiles in the exact sequence shown.\""
            },
            ["PATH_PUZZLE_COMPLETE"] = new[]
            {
                "\"Step 3: Security floor grid. Watch the floor tiles carefully.\"",
                "\"The illuminated tiles show the safe path across the grid.\"",
                "\"Step or click through the tiles in the exact sequence shown.\""
            },
            ["STAGE3_SYMBOL_SOLVED"] = new[]
            {
                "\"Step 4: Access the symbol terminal to finish clearing the room.\"",
                "\"Look at the wall monitors displaying symbol equations.\"",
                "\"Solve the values for each symbol and enter the master code into the box.\""
            },

            // ── Stage 4: Final Control Room ───────────────────────────────────
            ["STAGE4_PATTERN_SOLVED"] = new[]
            {
                "\"Analyze the pattern matrix at the central console.\"",
                "\"Align the terminal frequencies according to the schematic.\"",
                "\"Match the core alignment patterns to initialize unlock.\""
            },
            ["STAGE4_CORE_OVERRIDE"] = new[]
            {
                "\"The core system requires primary authorization override.\"",
                "\"Stabilize the power conduit cables to open the blast doors.\"",
                "\"Engage the final manual override sequence.\""
            },
            ["FINAL_EXIT_UNLOCKED"] = new[]
            {
                "\"All security barriers deactivated! Proceed to the extraction point!\""
            }
        };

        // ── Objective-change dialogues ─────────────────────────────────────────
        private static readonly Dictionary<string, string> ObjectiveStartDialogue =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Stage 1
            ["LOCKBOX_KEY_ACQUIRED"] =
                "\"Find the key first. I'll be here if you need me.\"",
            ["LOCKBOX_OPENED"] =
                "\"Good. Now try that key on the locked box.\"",
            ["EXIT_NOTE_READ"] =
                "\"Nice. There's something inside. Take a closer look.\"",
            ["EXIT_CODE_ACCEPTED"] =
                "\"That note has the code you need for the keypad.\"",

            // Stage 2
            ["CLUE_FOUND"] =
                "\"Storage bay ahead. Search the tables for symbol clues.\"",
            ["COMBO_DISCOVERED"] =
                "\"Examine the symbol combinations on that clue sheet.\"",
            ["CABINET_UNLOCKED"] =
                "\"Dial the symbol combination on the heavy locker.\"",
            ["ACCESS_CARD_ACQUIRED"] =
                "\"Grab the security card from the open cabinet.\"",
            ["CORRIDOR_UNLOCKED"] =
                "\"Swipe your card at the exit reader to proceed.\"",

            // Stage 3
            ["STAGE3_COLOR_MEMORY_SOLVED"] =
                "\"Start with the color panel lights on the primary console.\"",
            ["STAGE3_LOCKDOWN_RESOLVED"] =
                "\"Lockdown triggered! Hit the red emergency box to silence the alarm!\"",
            ["STAGE3_PATH_SOLVED"] =
                "\"Floor grid active. Memorize the path and traverse the tiles.\"",
            ["STAGE3_SYMBOL_SOLVED"] =
                "\"Final step: read the monitor formulas and crack the symbol lock!\""
        };

        // ── Completion acknowledgement dialogues ───────────────────────────────
        private static readonly Dictionary<string, string> CompletionDialogue =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Stage 1
            ["LOCKBOX_KEY_ACQUIRED"] =
                "\"Found it. Now use it on the lockbox.\"",
            ["LOCKBOX_OPENED"] =
                "\"Nice. There's something inside the box. Take a closer look.\"",
            ["EXIT_NOTE_READ"] =
                "\"That note contains the code. Enter it on the keypad terminal.\"",
            ["EXIT_CODE_ACCEPTED"] =
                "\"That's it. The exit is unlocked. Move quickly.\"",

            // Stage 2
            ["CLUE_FOUND"] =
                "\"Symbol document secured. Let's inspect the combination.\"",
            ["COMBO_DISCOVERED"] =
                "\"Symbols decoded. Ready for the supply cabinet.\"",
            ["CABINET_UNLOCKED"] =
                "\"Locker opened! Retrieve the keycard inside.\"",
            ["ACCESS_CARD_ACQUIRED"] =
                "\"Access card acquired. Head to the security reader.\"",
            ["CORRIDOR_UNLOCKED"] =
                "\"Security clearance granted. Proceeding to Stage 3!\"",

            // Stage 3
            ["STAGE3_COLOR_MEMORY_SOLVED"] =
                "\"Color sequence accepted! Warning: Lockdown initiated!\"",
            ["STAGE3_LOCKDOWN_RESOLVED"] =
                "\"Alarm disarmed! Security floor grid activated!\"",
            ["STAGE3_PATH_SOLVED"] =
                "\"Path navigated safely! Symbol lock terminal ready!\"",
            ["PATH_PUZZLE_COMPLETE"] =
                "\"Path completed! Head over to the symbol lock terminal!\"",
            ["STAGE3_SYMBOL_SOLVED"] =
                "\"All security locks bypassed! The blast door is opening!\""
        };

        // ── Timer warning dialogues ────────────────────────────────────────────
        private static readonly Dictionary<float, string> TimerWarningDialogue =
            new Dictionary<float, string>
        {
            [300f] = "\"You're halfway through the available time. Stay focused.\"",
            [120f] = "\"Two minutes remaining. Focus.\"",
            [60f]  = "\"One minute. If you're stuck, ask me for a hint.\"",
            [30f]  = "\"Thirty seconds.\""
        };

        // ── Lifecycle ─────────────────────────────────────────────────────────
        private void Awake()
        {
            if (instance != null && instance != this)
            {
                if (gameObject.name == "[CipherHostUI]")
                {
                    Destroy(gameObject);
                }
                else
                {
                    Destroy(this);
                }
                return;
            }
            instance = this;
            // Only DontDestroyOnLoad if this GameObject is exclusively a dedicated [CipherHostUI] instance!
            // Never mark shared scene manager GameObjects (e.g. GameManagers) as DontDestroyOnLoad!
            if (gameObject.name == "[CipherHostUI]")
            {
                DontDestroyOnLoad(gameObject);
            }
            EnsurePortraitLoaded();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoEnsureInstance()
        {
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (scene == "MainMenu") return;
            if (instance == null)
            {
                GameObject go = new GameObject("[CipherHostUI]");
                go.AddComponent<CipherHostUI>();
            }
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void OnEnable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;

            if (ObjectiveManager.Instance != null)
            {
                ObjectiveManager.Instance.OnStepChanged    += HandleStepChanged;
                ObjectiveManager.Instance.OnObjectiveCompleted += HandleObjectiveCompleted;
                ObjectiveManager.Instance.OnAllObjectivesCompleted += HandleAllCompleted;
            }

            if (StageTimer.Instance != null)
            {
                StageTimer.Instance.OnWarningThreshold += HandleTimerWarning;
                StageTimer.Instance.OnTimerExpired     += HandleTimerExpired;
            }
        }

        private void OnDisable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;

            if (ObjectiveManager.Instance != null)
            {
                ObjectiveManager.Instance.OnStepChanged    -= HandleStepChanged;
                ObjectiveManager.Instance.OnObjectiveCompleted -= HandleObjectiveCompleted;
                ObjectiveManager.Instance.OnAllObjectivesCompleted -= HandleAllCompleted;
            }

            if (StageTimer.Instance != null)
            {
                StageTimer.Instance.OnWarningThreshold -= HandleTimerWarning;
                StageTimer.Instance.OnTimerExpired     -= HandleTimerExpired;
            }
        }

        private void Start()
        {
            // Re-bind in case singletons weren't ready in OnEnable
            if (ObjectiveManager.Instance != null)
            {
                ObjectiveManager.Instance.OnStepChanged       -= HandleStepChanged;
                ObjectiveManager.Instance.OnStepChanged       += HandleStepChanged;
                ObjectiveManager.Instance.OnObjectiveCompleted -= HandleObjectiveCompleted;
                ObjectiveManager.Instance.OnObjectiveCompleted += HandleObjectiveCompleted;
                ObjectiveManager.Instance.OnAllObjectivesCompleted -= HandleAllCompleted;
                ObjectiveManager.Instance.OnAllObjectivesCompleted += HandleAllCompleted;
            }
            if (StageTimer.Instance != null)
            {
                StageTimer.Instance.OnWarningThreshold -= HandleTimerWarning;
                StageTimer.Instance.OnWarningThreshold += HandleTimerWarning;
                StageTimer.Instance.OnTimerExpired     -= HandleTimerExpired;
                StageTimer.Instance.OnTimerExpired     += HandleTimerExpired;
            }

            // Auto-activate Cipher if gameplay is in progress
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (sceneName != "MainMenu")
            {
                if (!AtticBriefingUI.IsBriefingOpen && !PhoneIntroUI.IsIntroOpen && !isActive)
                {
                    Activate();
                }
                else if (isActive)
                {
                    // Already active from previous level
                }
            }
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (scene.name == "MainMenu")
            {
                isActive = false;
                isDialogueOpen = false;
                isHintOfferOpen = false;
                return;
            }

            isActive = true;
            currentHintLevel = 0;
            inactivityTimer = 0f;
            hintCooldownTimer = 8f;

            // Re-bind to singletons in new scene
            if (ObjectiveManager.Instance != null)
            {
                ObjectiveManager.Instance.OnStepChanged       -= HandleStepChanged;
                ObjectiveManager.Instance.OnStepChanged       += HandleStepChanged;
                ObjectiveManager.Instance.OnObjectiveCompleted -= HandleObjectiveCompleted;
                ObjectiveManager.Instance.OnObjectiveCompleted += HandleObjectiveCompleted;
                ObjectiveManager.Instance.OnAllObjectivesCompleted -= HandleAllCompleted;
                ObjectiveManager.Instance.OnAllObjectivesCompleted += HandleAllCompleted;
            }

            if (StageTimer.Instance != null)
            {
                StageTimer.Instance.OnWarningThreshold -= HandleTimerWarning;
                StageTimer.Instance.OnWarningThreshold += HandleTimerWarning;
                StageTimer.Instance.OnTimerExpired     -= HandleTimerExpired;
                StageTimer.Instance.OnTimerExpired     += HandleTimerExpired;
            }

            string greeting = GetRoomGreeting(scene.name);
            if (!string.IsNullOrEmpty(greeting))
            {
                StartCoroutine(DelayedMessage(greeting, 1.6f));
            }
        }

        private string GetRoomGreeting(string sceneName)
        {
            switch (sceneName)
            {
                case "StorageRoom":
                    return "\"Storage Bay reached. Look for symbol markings and locked supply lockers.\"";
                case "SecurityControlRoom":
                    return "\"Security Control Room. Clear the systems in sequence: Color lights, Red box, Grid path, then Symbol terminal.\"";
                case "FinalControlRoom":
                    return "\"Final Control Center. This is the last stretch — override the core systems to break out!\"";
                case "VaultCellar":
                    return "\"Vault Cellar reached. Keep your senses keen, we are nearing the final door.\"";
                default:
                    return null;
            }
        }

        private void Update()
        {
            if (!isActive) return;

            // Hotkey H to ask Cipher for a hint directly
            bool hPressed = false;
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.hKey.wasPressedThisFrame)
            {
                hPressed = true;
            }
            try
            {
                if (!hPressed && Input.GetKeyDown(KeyCode.H))
                {
                    hPressed = true;
                }
            }
            catch {}

            if (hPressed)
            {
                GiveHint();
            }

            // Keep panel alpha fully visible as persistent companion card
            panelAlpha = Mathf.MoveTowards(panelAlpha, 1f, Time.unscaledDeltaTime * 4f);

            // Dialogue timer
            if (isDialogueOpen)
            {
                dialogueTimer -= Time.deltaTime;
                if (dialogueTimer <= 0f)
                {
                    isDialogueOpen = false;
                }
            }

            // Inactivity tracker (periodically offers guidance if stuck)
            if (!isDialogueOpen && hintCooldownTimer <= 0f)
            {
                inactivityTimer += Time.deltaTime;
                if (inactivityTimer >= inactivityTimeout)
                {
                    inactivityTimer = 0f;
                    hintCooldownTimer = hintOfferCooldown;
                    OfferHintPrompt();
                }
            }

            if (hintCooldownTimer > 0f)
                hintCooldownTimer -= Time.deltaTime;
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Activate Cipher after gameplay begins (called by AtticBriefingUI or game start).</summary>
        public void Activate()
        {
            isActive = true;
            currentHintLevel = 0;
            inactivityTimer  = 0f;
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (sceneName == "EscapeRoom_Main")
            {
                ShowMessage("\"You're finally awake.\"\n\"Don't worry. You're not in danger.\"\n\"Find your way out and I'll help you with what comes next.\"",
                            duration: 7f);
            }
            else
            {
                string greeting = GetRoomGreeting(sceneName);
                if (!string.IsNullOrEmpty(greeting))
                    ShowMessage(greeting, duration: 6f);
            }
            Debug.Log("[CipherHostUI] Cipher activated.");
        }

        /// <summary>Show a Cipher dialogue message for the given duration.</summary>
        public void ShowMessage(string message, float duration = -1f)
        {
            currentMessage  = message;
            dialogueTimer   = duration > 0f ? duration : messageDisplayDuration;
            isDialogueOpen  = true;
            isHintOfferOpen = false;
            inactivityTimer = 0f;
        }

        /// <summary>Give the next level hint for the current objective. Cycles through 3 levels.</summary>
        public void GiveHint()
        {
            isHintOfferOpen = false;
            var step = ObjectiveManager.Instance?.CurrentStep;
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

            if (step != null && !string.IsNullOrEmpty(step.CompletionFlag) && HintsByFlag.TryGetValue(step.CompletionFlag, out string[] hints))
            {
                int idx = Mathf.Clamp(currentHintLevel, 0, hints.Length - 1);
                ShowMessage(hints[idx], duration: messageDisplayDuration + 2f);
                if (currentHintLevel < hints.Length - 1) currentHintLevel++;
            }
            else
            {
                string fallback = GetFallbackHintForScene(sceneName);
                ShowMessage(fallback, duration: messageDisplayDuration + 2f);
            }

            inactivityTimer   = 0f;
            hintCooldownTimer = hintOfferCooldown * 0.5f;
        }

        private string GetFallbackHintForScene(string sceneName)
        {
            switch (sceneName)
            {
                case "EscapeRoom_Main":
                    return "\"Check the desk drawers and table for keys, then inspect the lockbox and keypad.\"";
                case "StorageRoom":
                    return "\"Search the storage workbenches for notes showing symbol combinations to unlock the supply locker.\"";
                case "SecurityControlRoom":
                    return "\"Follow the exact sequence: Color lights panel -> Red alarm box -> Floor grid memory -> Symbol monitor console.\"";
                case "FinalControlRoom":
                case "VaultCellar":
                    return "\"Inspect the core control consoles and align the pattern matrix to open the final blast doors.\"";
                default:
                    return "\"Survey the room closely. Every puzzle has a clue hidden nearby.\"";
            }
        }

        /// <summary>Called by the [ASK CIPHER] button.</summary>
        public void OnAskCipherClicked()
        {
            GiveHint();
        }

        // ── Event Handlers ────────────────────────────────────────────────────

        private void HandleStepChanged(int stepIndex, ObjectiveStep step)
        {
            if (!isActive || step == null) return;
            currentHintLevel = 0;
            inactivityTimer  = 0f;

            if (ObjectiveStartDialogue.TryGetValue(step.CompletionFlag, out string msg))
            {
                StartCoroutine(DelayedMessage(msg, 1.2f));
            }
        }

        private void HandleObjectiveCompleted(ObjectiveStep step)
        {
            if (!isActive || step == null) return;
            currentHintLevel = 0;
            inactivityTimer  = 0f;

            if (CompletionDialogue.TryGetValue(step.CompletionFlag, out string msg))
            {
                StartCoroutine(DelayedMessage(msg, 0.8f));
            }
        }

        private void HandleAllCompleted()
        {
            if (!isActive) return;
            // Stop timer
            StageTimer.Instance?.StopTimer();
            string remaining = StageTimer.Instance != null
                ? StageTimer.FormatTime(StageTimer.Instance.TimeRemaining)
                : "--:--";
            ShowMessage($"\"Excellent work.\"\n\"Time remaining: {remaining}\"\n\"That was only the first room.\"",
                        duration: 8f);
        }

        private void HandleTimerWarning(float threshold)
        {
            if (!isActive) return;
            if (TimerWarningDialogue.TryGetValue(threshold, out string msg))
            {
                StartCoroutine(DelayedMessage(msg, 0.5f));
            }
        }

        private void HandleTimerExpired()
        {
            if (!isActive) return;
            isDialogueOpen  = false;
            isHintOfferOpen = false;
            isActive        = false;
        }

        private IEnumerator DelayedMessage(string msg, float delay)
        {
            yield return new WaitForSeconds(delay);
            ShowMessage(msg);
        }

        private void OfferHintPrompt()
        {
            if (!isActive || isDialogueOpen) return;
            ShowMessage("\"Need a hint? Press [H] whenever you're ready.\"", duration: 5.5f);
        }

        // ── OnGUI ─────────────────────────────────────────────────────────────
        private void OnGUI()
        {
            // Suppress during non-gameplay screens
            if (AtticBriefingUI.IsBriefingOpen) return;
            if (PhoneIntroUI.IsIntroOpen || EscapeRoom.Stage2.Stage2IntroUI.IsIntroOpen) return;
            if (PauseManager.Instance != null && PauseManager.Instance.IsPaused) return;
            if (EscapeUI.Instance != null && EscapeUI.Instance.IsOpen) return;

            if (!isActive) return;

            if (!stylesBuilt) BuildStyles();

            // ── Ask Cipher button — always visible during active gameplay ──────
            DrawAskCipherButton();

            // ── Cipher corner companion card — always visible and non-obstructive ──
            float eff = Mathf.Clamp01(panelAlpha);
            if (eff > 0.02f)
            {
                DrawPanel(eff);
            }
        }

        private void DrawPanel(float alpha)
        {
            EnsurePortraitLoaded();
            float sw = Screen.width, sh = Screen.height;

            // Compact HUD companion card in bottom-left corner
            // Snug, non-obtrusive (360 x 96), doesn't block player view or lower floor
            float pw = Mathf.Min(360f, sw * 0.40f);
            float ph = 96f;
            float px = 16f;
            float py = sh - ph - 42f; // leaves space for [H] ASK CIPHER button directly beneath

            Color old = GUI.color;
            int oldDepth = GUI.depth;
            GUI.depth = 100; // never steal input focus or click priority

            // Semi-transparent dark backing
            GUI.color = new Color(0.04f, 0.08f, 0.12f, 0.75f * alpha);
            GUI.DrawTexture(new Rect(px, py, pw, ph), Texture2D.whiteTexture);

            // Cyan left accent border
            GUI.color = accentColor * new Color(1, 1, 1, 0.90f * alpha);
            GUI.DrawTexture(new Rect(px, py, 3f, ph), Texture2D.whiteTexture);

            // Portrait: 64x76
            float portW = 64f;
            float portH = 76f;
            float portX = px + 10f;
            float portY = py + 10f;

            // Border frame around portrait
            GUI.color = accentColor * new Color(1, 1, 1, 0.80f * alpha);
            GUI.DrawTexture(new Rect(portX - 2, portY - 2, portW + 4, portH + 4), Texture2D.whiteTexture);

            // Inner dark backdrop
            GUI.color = new Color(0.02f, 0.04f, 0.06f, alpha);
            GUI.DrawTexture(new Rect(portX, portY, portW, portH), Texture2D.whiteTexture);

            if (portraitTexture != null)
            {
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.DrawTexture(new Rect(portX, portY, portW, portH), portraitTexture, ScaleMode.ScaleToFit);
            }
            else
            {
                GUI.color = accentColor * new Color(1, 1, 1, alpha);
                float cx = portX + portW * 0.5f, cy = portY + portH * 0.5f;
                GUI.DrawTexture(new Rect(cx - 2, cy - 14, 4, 28), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx - 14, cy - 2, 28, 4), Texture2D.whiteTexture);
            }

            GUI.color = new Color(1f, 1f, 1f, alpha);

            // Name label: CIPHER · DIGITAL GUIDE
            float textX = px + portW + 16f;
            float textW = pw - (portW + 24f);
            nameStyle.normal.textColor = accentColor * new Color(1, 1, 1, alpha);
            GUI.Label(new Rect(textX, py + 8f, textW, 20f), hostName + "  ·  DIGITAL GUIDE", nameStyle);

            // Divider line
            GUI.color = accentColor * new Color(1, 1, 1, 0.35f * alpha);
            GUI.DrawTexture(new Rect(textX, py + 29f, textW - 4f, 1), Texture2D.whiteTexture);

            GUI.color = new Color(1f, 1f, 1f, alpha);

            // Content message: either active dialogue or subtle idle guide text
            string displayMsg;
            if (isDialogueOpen && !string.IsNullOrEmpty(currentMessage))
            {
                displayMsg = currentMessage;
                msgStyle.normal.textColor = new Color(1.0f, 1.0f, 1.0f, alpha);
            }
            else
            {
                displayMsg = GetIdleGuidance();
                msgStyle.normal.textColor = new Color(0.70f, 0.85f, 0.95f, 0.85f * alpha);
            }

            GUI.Label(new Rect(textX, py + 33f, textW, ph - 38f), displayMsg, msgStyle);

            GUI.depth = oldDepth;
            GUI.color = old;
        }

        private string GetIdleGuidance()
        {
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            switch (sceneName)
            {
                case "EscapeRoom_Main":
                    return "\"Attic escape active. Press [H] for clue.\"";
                case "StorageRoom":
                    return "\"Storage bay active. Press [H] for clue.\"";
                case "SecurityControlRoom":
                    return "\"Security control active. Press [H] for clue.\"";
                case "FinalControlRoom":
                    return "\"Final control room. Press [H] for clue.\"";
                default:
                    return "\"Online. Press [H] for clue.\"";
            }
        }

        private void DrawAskCipherButton()
        {
            if (!isActive) return;

            // Suppress during modals
            if (KeypadUI.Instance != null && KeypadUI.Instance.IsOpen) return;
            if (ClueInteractable.IsAnyClueOpen) return;
            if (SymbolClueInteractable.IsAnyClueOpen) return;
            if (EscapeUI.Instance != null && EscapeUI.Instance.IsOpen) return;

            if (!stylesBuilt) BuildStyles();

            string buttonLabel = "[ H ] ASK CIPHER";

            float bw = 140f, bh = 26f;
            float bx = 16f;
            float by = Screen.height - bh - 10f;

            Color old = GUI.color;

            GUI.color = new Color(0.05f, 0.08f, 0.12f, 0.85f);
            GUI.DrawTexture(new Rect(bx, by, bw, bh), Texture2D.whiteTexture);
            GUI.color = accentColor * new Color(1, 1, 1, 0.85f);
            GUI.DrawTexture(new Rect(bx, by, 3f, bh), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.backgroundColor = new Color(0.08f, 0.12f, 0.16f, 0.95f);
            var askStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize  = 11, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = Color.white }
            };
            if (GUI.Button(new Rect(bx, by, bw, bh), buttonLabel, askStyle))
            {
                OnAskCipherClicked();
            }
            GUI.backgroundColor = Color.white;
            GUI.color = old;
        }

        // ── Styles ────────────────────────────────────────────────────────────
        private void BuildStyles()
        {
            nameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 11, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            msgStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 10, fontStyle = FontStyle.Normal,
                alignment = TextAnchor.UpperLeft, wordWrap = true
            };
            btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize  = 12, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = Color.white }
            };
            smallBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize  = 10, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = Color.white }
            };
            stylesBuilt = true;
        }
    }
}
