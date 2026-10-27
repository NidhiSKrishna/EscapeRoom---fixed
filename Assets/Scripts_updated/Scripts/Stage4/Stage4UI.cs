using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Stage 4 - Dedicated UI and HUD (spec sections 34, 35, 38, 39).
///
/// Implements standard HUD methods (spec section 39):
///   - public void ShowMessage(string message)
///   - public void ShowWarning(string message)
///   - public void ShowSuccess(string message)
///   - public void ShowObjective(string message)
///   - public void HideMessage()
///
/// Renders:
///   - Stage title: STAGE 4 - FINAL CONTROL ROOM
///   - Dynamic objective indicator
///   - Main stage 8-minute countdown clock
///   - Message banners (Success, Warning, Info)
///   - Final Escape climax screen with Cipher dialogue and stats (Total Time)
/// </summary>
public class Stage4UI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Stage4Manager manager;
    [SerializeField] private Stage4TimerAdapter timerAdapter;
    [SerializeField] private Stage4SecurityLockdown lockdown;
    [SerializeField] private Stage4IntegrationAdapter integrationAdapter;

    [Header("Instruction timing (seconds)")]
    [Tooltip("Mission briefing shown once, when the stage first starts.")]
    [SerializeField] private float briefingSeconds = 15f;
    [Tooltip("How long the Stage title and Objective box stay visible after each task is completed (seconds).")]
    [SerializeField] private float objectiveDisplaySeconds = 4f;
    [Tooltip("Keypad (objective 5) instructions stay on screen at least this long.")]
    [SerializeField] private float codeInstructionSeconds = 20f;
    [Tooltip("HOW TO PLAY box for the other objectives.")]
    [SerializeField] private float defaultInstructionSeconds = 10f;
    [Tooltip("How long the box shows when the player presses H.")]
    [SerializeField] private float helpKeySeconds = 15f;

    [Header("Cipher Dialogue timing")]
    [Tooltip("How long Cipher dialogue popups stay on screen (seconds).")]
    [SerializeField] private float cipherDialogueSeconds = 5f;

    [Header("Sequence Delays (staggers UI boxes so they do not overlap)")]
    [Tooltip("Pause after a puzzle modal closes before showing the solved / digit reminder banner (seconds).")]
    [SerializeField] private float delayBeforeBanner = 0.4f;
    [Tooltip("Pause between banner ending and Cipher dialogue starting (seconds).")]
    [SerializeField] private float delayBeforeCipher = 0.4f;
    [Tooltip("Pause between Cipher dialogue ending and HOW TO PLAY box starting (seconds).")]
    [SerializeField] private float delayBeforeInstruction = 0.4f;

    [Header("Congratulations screen")]
    [Tooltip("How long the CONGRATULATIONS screen stays before the Cipher ending appears.")]
    [SerializeField] private float congratsSeconds = 6f;

    [Header("Game over (time ran out)")]
    [Tooltip("How long the GAME OVER screen is shown before the game exits.")]
    [SerializeField] private float gameOverSeconds = 8f;
    [Tooltip("ON: close the game (or stop Play mode in the Editor) after the GAME OVER screen. " +
             "Main project: turn OFF and use the adapter's On Game Over Exit event instead.")]
    [SerializeField] private bool quitOnGameOver = true;

    [Header("Code digit reminders")]
    [Tooltip("ON = after each puzzle, a banner says which digit of the keypad code it gave and what it is. " +
             "OFF = harder: the player must work it out alone.")]
    [SerializeField] private bool showDigitReminders = true;
    [SerializeField] private float digitReminderSeconds = 7f;

    [Header("Cipher Character")]
    [SerializeField] private Texture2D cipherPortrait;

    // Spec Section 38 Color Palette
    private static readonly Color ColorNormal = new Color(1f, 1f, 1f);       // #FFFFFF
    private static readonly Color ColorDanger = new Color(1f, 0.2f, 0.2f);    // #FF3333
    private static readonly Color ColorSuccess = new Color(0.27f, 1f, 0.4f);  // #44FF66
    private static readonly Color ColorWarning = new Color(1f, 0.82f, 0.25f); // #FFD23F

    private string currentObjective = "OBJECTIVE: Access Pattern Terminal";
    private float objectiveUntil = 0f;
    private string currentInstruction = "HOW TO PLAY: Press E on the Pattern Terminal. Figure out the number sequence pattern and submit the missing number.";
    private float instructionUntil = 0f;
    private bool briefingShown = false;
    private float briefingUntil = 0f;
    private string lastInstruction = "";
    private GUIStyle instrStyle, instrBoxStyle, briefTitleStyle, briefBodyStyle, briefHintStyle, tinyHintStyle;
    private string bannerMessage = "";
    private Color bannerColor = ColorNormal;
    private float bannerUntil = 0f;

    // Cipher In-game Dialogue popup
    private string cipherActiveDialogue = "";
    private float cipherDialogueUntil = 0f;

    private bool showEscapeScreen = false;
    private float congratsUntil = 0f;
    private bool showGameOver = false;
    private float gameOverEndsAt = 0f;
    private bool gameOverExited = false;
    private int dialogueStep = 0;
    private static readonly string[] CipherDialogueLines =
    {
        "\"You did it.\"",
        "\"The facility is open.\"",
        "\"But now we know what happened.\"",
        "\"Someone didn't shut this place down.\"",
        "\"They locked it.\""
    };

    private GUIStyle headerStyle, objStyle, instructionStyle, timerStyle, bannerStyle;
    private GUIStyle escapeTitleStyle, escapeSubStyle, escapeStatStyle, dialogueStyle;
    private GUIStyle cipherBoxStyle, cipherNameStyle;

    public static Stage4UI Instance { get; private set; }

    public static bool IsAnyOverlayBlocking
    {
        get
        {
            if (Instance != null)
            {
                if (Instance.BriefingVisible) return true;
                if (Instance.showEscapeScreen || Instance.showGameOver) return true;
            }
            return false;
        }
    }

    private void Awake()
    {
        Instance = this;
        if (manager == null) manager = FindFirstObjectByType<Stage4Manager>();
        if (timerAdapter == null) timerAdapter = FindFirstObjectByType<Stage4TimerAdapter>();
        if (lockdown == null) lockdown = FindFirstObjectByType<Stage4SecurityLockdown>();
        if (integrationAdapter == null) integrationAdapter = FindFirstObjectByType<Stage4IntegrationAdapter>();

        LoadCipherPortrait();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void LoadCipherPortrait()
    {
        if (cipherPortrait != null) return;
#if UNITY_EDITOR
        cipherPortrait = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/CipherAvatar.jpg");
#endif
        if (cipherPortrait == null)
        {
            byte[] bytes = System.IO.File.Exists("Assets/Textures/CipherAvatar.jpg")
                ? System.IO.File.ReadAllBytes("Assets/Textures/CipherAvatar.jpg")
                : null;
            if (bytes != null)
            {
                cipherPortrait = new Texture2D(2, 2);
                cipherPortrait.LoadImage(bytes);
            }
        }
    }

    private void OnEnable()
    {
        if (manager != null)
        {
            manager.OnStateChanged += HandleStateChanged;
            manager.OnEscapeCompleted += HandleEscapeCompleted;
            manager.OnStageReset += HandleStageReset;
        }

        if (integrationAdapter != null)
        {
            integrationAdapter.OnCipherDialogueRequested += ShowCipherDialogue;
            integrationAdapter.OnCipherHintRequested += ShowCipherDialogue;
        }
    }

    private void OnDisable()
    {
        if (stateSequenceCoroutine != null)
        {
            StopCoroutine(stateSequenceCoroutine);
            stateSequenceCoroutine = null;
        }

        if (manager != null)
        {
            manager.OnStateChanged -= HandleStateChanged;
            manager.OnEscapeCompleted -= HandleEscapeCompleted;
            manager.OnStageReset -= HandleStageReset;
        }

        if (integrationAdapter != null)
        {
            integrationAdapter.OnCipherDialogueRequested -= ShowCipherDialogue;
            integrationAdapter.OnCipherHintRequested -= ShowCipherDialogue;
        }
    }

    public void ShowCipherDialogue(string message)
    {
        ShowCipherDialogue(message, cipherDialogueSeconds);
    }

    public void ShowCipherDialogue(string message, float seconds)
    {
        if (EscapeRoom.UI.CipherHostUI.Instance != null)
        {
            EscapeRoom.UI.CipherHostUI.Instance.ShowMessage(message, seconds);
        }
        cipherActiveDialogue = message;
        cipherDialogueUntil = Time.unscaledTime + seconds;
    }

    public void ShowInstruction(string instruction)
    {
        ShowInstruction(instruction, defaultInstructionSeconds);
    }

    /// <summary>Shows the HOW TO PLAY box under the objective for the given number of seconds.</summary>
    public void ShowInstruction(string instruction, float seconds)
    {
        currentInstruction = instruction;
        lastInstruction = instruction;
        instructionUntil = Time.unscaledTime + seconds;
    }

    private bool BriefingVisible
    {
        get { return briefingUntil > 0f && Time.unscaledTime < briefingUntil; }
    }

    private bool InstructionVisible
    {
        get { return !string.IsNullOrEmpty(currentInstruction) && Time.unscaledTime < instructionUntil; }
    }

    private bool BannerVisible
    {
        get { return !string.IsNullOrEmpty(bannerMessage) && Time.unscaledTime < bannerUntil; }
    }

    private bool CipherDialogueVisible
    {
        get { return !string.IsNullOrEmpty(cipherActiveDialogue) && Time.unscaledTime < cipherDialogueUntil; }
    }

    /// <summary>Shows the Stage title + Objective box now, for objectiveDisplaySeconds.</summary>
    private void QueueObjectiveDisplay()
    {
        objectiveUntil = Time.unscaledTime + objectiveDisplaySeconds;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            // Allow dismissing the Mission Briefing early so the player can start playing immediately
            if (BriefingVisible && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame))
            {
                briefingUntil = 0f;
            }

            // H = show the current instructions and objective again
            if (keyboard.hKey.wasPressedThisFrame && !string.IsNullOrEmpty(lastInstruction))
            {
                currentInstruction = lastInstruction;
                instructionUntil = Time.unscaledTime + helpKeySeconds;
                QueueObjectiveDisplay();
            }
            if (showEscapeScreen && keyboard.qKey.wasPressedThisFrame)
            {
                QuitApplication();
            }
        }
    }

    private string BuildBriefingText()
    {
        int minutes = manager != null ? Mathf.RoundToInt(manager.StageDurationSeconds / 60f) : 8;
        return "You have " + minutes + " minutes to escape. Solve 5 puzzles, one after the other:\n\n" +
               "1. Pattern terminal      2. Memory lights      3. Control panels\n" +
               "4. Wire hack      5. Security keypad\n\n" +
               "IMPORTANT: the last keypad needs a 4-digit code built from your EARLIER answers.\n" +
               "Remember them, or write them down, as you go:\n" +
               "   - the number you find in the Pattern puzzle\n" +
               "   - the colours of the Memory lights (especially the FIRST and the LAST)\n" +
               "   - which Control Panel you choose\n" +
               "   - which wire terminal you connect LAST\n\n" +
               "Walk up to a console and press E to use it. A wrong answer costs you time.";
    }

    private string BuildCodeInstruction()
    {
        int terminals = 5;
        if (manager != null && manager.Generator != null) terminals = manager.Generator.GetWireCount();

        return "SECURITY KEYPAD - build the 4-digit code from your earlier results:\n\n" +
               "Digit 1: the number you entered in the PATTERN puzzle (0-9)\n\n" +
               "Digit 2: colour of the LAST light in the MEMORY puzzle\n" +
               "   (Red 1, Blue 2, Green 3, Yellow 4)\n\n" +
               "Digit 3: the CONTROL PANEL that matched the FIRST light\n" +
               "   (A 1, B 2, C 3, D 4)\n\n" +
               "Digit 4: the terminal you connected LAST in the WIRE puzzle (1-" + terminals + ")\n\n" +
               "Example: 7, 3, 2, 4 -> type 7324.\n" +
               "Press E on the Keypad to enter it. Press H to read this again.";
    }

    private Coroutine stateSequenceCoroutine = null;

    private void TriggerStateSequence(
        string objectiveText,
        string instructionText,
        float instructionDuration,
        string bannerFallback,
        string digitReminderText,
        string cipherText,
        bool isStartBriefing = false)
    {
        if (stateSequenceCoroutine != null)
        {
            StopCoroutine(stateSequenceCoroutine);
            stateSequenceCoroutine = null;
        }
        stateSequenceCoroutine = StartCoroutine(StateSequenceRoutine(
            objectiveText,
            instructionText,
            instructionDuration,
            bannerFallback,
            digitReminderText,
            cipherText,
            isStartBriefing));
    }

    private IEnumerator StateSequenceRoutine(
        string objectiveText,
        string instructionText,
        float instructionDuration,
        string bannerFallback,
        string digitReminderText,
        string cipherText,
        bool isStartBriefing)
    {
        // 1. Immediately update the main Objective banner
        if (!string.IsNullOrEmpty(objectiveText))
        {
            ShowObjective(objectiveText);
        }

        // Cache the instruction so pressing H works at any moment
        if (!string.IsNullOrEmpty(instructionText))
        {
            lastInstruction = instructionText;
        }

        // 2. Initial Mission Briefing (first start only)
        if (isStartBriefing && !briefingShown)
        {
            briefingShown = true;
            briefingUntil = Time.unscaledTime + briefingSeconds;
            // Clear any old popups while briefing is active
            HideMessage();
            cipherActiveDialogue = "";
            cipherDialogueUntil = 0f;
            currentInstruction = "";
            instructionUntil = 0f;

            while (BriefingVisible)
            {
                yield return null;
            }
            yield return new WaitForSecondsRealtime(0.2f);
            QueueObjectiveDisplay();
        }

        // 3. Result / Digit Reminder Banner (if any)
        if (!string.IsNullOrEmpty(digitReminderText) || !string.IsNullOrEmpty(bannerFallback))
        {
            // Wait until any active interactive puzzle modal (e.g. Pattern sequence terminal) has fully closed
            while (Stage4TestPlayer.InputLocked)
            {
                yield return null;
            }

            if (delayBeforeBanner > 0f)
            {
                yield return new WaitForSecondsRealtime(delayBeforeBanner);
            }

            // Task completed: show the stage title and new objective box
            QueueObjectiveDisplay();

            ShowDigitBanner(bannerFallback, digitReminderText);
            while (Time.unscaledTime < bannerUntil)
            {
                yield return null;
            }
            if (delayBeforeCipher > 0f)
            {
                yield return new WaitForSecondsRealtime(delayBeforeCipher);
            }
        }

        // 4. Cipher Dialogue popup (if any)
        if (!string.IsNullOrEmpty(cipherText))
        {
            ShowCipherDialogue(cipherText, cipherDialogueSeconds);
            while (Time.unscaledTime < cipherDialogueUntil)
            {
                yield return null;
            }
            if (delayBeforeInstruction > 0f)
            {
                yield return new WaitForSecondsRealtime(delayBeforeInstruction);
            }
        }

        // 5. "HOW TO PLAY" Box
        if (!string.IsNullOrEmpty(instructionText))
        {
            ShowInstruction(instructionText, instructionDuration);
        }

        stateSequenceCoroutine = null;
    }

    private void HandleStateChanged(Stage4State state)
    {
        switch (state)
        {
            case Stage4State.PatternPuzzle:
                TriggerStateSequence(
                    objectiveText: "OBJECTIVE 1/5: Analyze & Solve Pattern Recognition Terminal",
                    instructionText: "HOW TO PLAY: Press E on the Pattern Terminal. Figure out the number sequence pattern and submit the missing number (0-9). Your answer is DIGIT 1 of the final keypad code.",
                    instructionDuration: defaultInstructionSeconds,
                    bannerFallback: null,
                    digitReminderText: null,
                    cipherText: "\"Access the Pattern Terminal. Discover the mathematical sequence rule to begin unlocking the system.\"",
                    isStartBriefing: true
                );
                break;

            case Stage4State.MemoryPuzzle:
                TriggerStateSequence(
                    objectiveText: "OBJECTIVE 2/5: Replay & Verify Memory Sequence Lights",
                    instructionText: "HOW TO PLAY: Press E on any colored button to watch the light sequence flash with sound. Then press the colored buttons in the exact same order. The colour number of the LAST light (Red 1, Blue 2, Green 3, Yellow 4) is DIGIT 2 of the keypad code. Also remember the FIRST light.",
                    instructionDuration: defaultInstructionSeconds,
                    bannerFallback: "Pattern Solved! Memory Terminal Unlocked.",
                    digitReminderText: DigitTextAfterPattern(),
                    cipherText: "\"Memory station active. Press E on the panel to watch the lights flash, then repeat the exact color sequence.\""
                );
                break;

            case Stage4State.ControlPanel:
                TriggerStateSequence(
                    objectiveText: "OBJECTIVE 3/5: Identify & Authorize Matching Control Console",
                    instructionText: "HOW TO PLAY: Recall the 1st light color that flashed in the Memory Puzzle. Find the console (A, B, C or D) showing that colour and press E. That console's number (A=1, B=2, C=3, D=4) is DIGIT 3 of the keypad code.",
                    instructionDuration: defaultInstructionSeconds,
                    bannerFallback: "Memory Verified! Control Consoles Activated.",
                    digitReminderText: DigitTextAfterMemory(),
                    cipherText: "\"Think back to the FIRST light color in the memory sequence. Authorize the console that matches it.\""
                );
                break;

            case Stage4State.PathPuzzle:
                TriggerStateSequence(
                    objectiveText: "OBJECTIVE 4/5: Rewire the Power Junction Box",
                    instructionText: "HOW TO PLAY: Press E on the Junction Box. Connect the terminals from the LOWEST to the HIGHEST voltage. The number of the terminal you connect LAST is DIGIT 4 of the keypad code.",
                    instructionDuration: defaultInstructionSeconds,
                    bannerFallback: "Console Authorized! Wiring Panel Online.",
                    digitReminderText: DigitTextAfterPanel(),
                    cipherText: "\"The power lines are scrambled. Open the Junction Box and reconnect them from the lowest voltage to the highest. A wrong wire shorts the circuit.\""
                );
                break;

            case Stage4State.CodePuzzle:
                TriggerStateSequence(
                    objectiveText: "OBJECTIVE 5/5: Enter 4-Digit Security Keypad Code",
                    instructionText: BuildCodeInstruction(),
                    instructionDuration: codeInstructionSeconds,
                    bannerFallback: "Power Restored! Security Keypad Ready.",
                    digitReminderText: DigitTextAfterWires(),
                    cipherText: "\"The final override code is built from all your puzzle results. Check your recipe clues if you need help.\""
                );
                break;

            case Stage4State.FinalOverride:
                TriggerStateSequence(
                    objectiveText: "FINAL STAGE: Initiate Central Override at Control Terminal",
                    instructionText: "HOW TO PLAY: Press E on the Central Terminal and confirm all 5 override steps. Prepare for emergency alarm!",
                    instructionDuration: defaultInstructionSeconds,
                    bannerFallback: "Keypad Accepted! Central Terminal Authorized.",
                    digitReminderText: null,
                    cipherText: "\"Central terminal unlocked! Execute the override sequence, but stay ready for whatever security throws at us.\""
                );
                break;

            case Stage4State.Lockdown:
                if (stateSequenceCoroutine != null)
                {
                    StopCoroutine(stateSequenceCoroutine);
                    stateSequenceCoroutine = null;
                }
                ShowObjective("CRITICAL: Complete Emergency Actions & Escape!");
                ShowWarning("SECURITY LOCKDOWN TRIGGERED!");
                ShowInstruction("HOW TO PLAY: 30 SECONDS LEFT! Run to the 3 Emergency Stations (Power, Matrix, Auth) and press E on each. When all 3 turn green, the exit door opens: walk through it to escape!");
                ShowCipherDialogue("\"EMERGENCY LOCKDOWN! Activate all three emergency stations right now before the clock runs out!\"");
                break;

            case Stage4State.Escaped:
                if (stateSequenceCoroutine != null)
                {
                    StopCoroutine(stateSequenceCoroutine);
                    stateSequenceCoroutine = null;
                }
                ShowObjective("FACILITY ACCESS OVERRIDE SUCCESSFUL");
                ShowInstruction("STATUS: Blast door breached. Facility successfully escaped.");
                break;

            case Stage4State.Failed:
                if (stateSequenceCoroutine != null)
                {
                    StopCoroutine(stateSequenceCoroutine);
                    stateSequenceCoroutine = null;
                }
                ShowObjective("GAME OVER - TIME EXPIRED");
                StartGameOver();
                break;
        }
    }

    private void HandleEscapeCompleted()
    {
        if (stateSequenceCoroutine != null)
        {
            StopCoroutine(stateSequenceCoroutine);
            stateSequenceCoroutine = null;
        }
        showEscapeScreen = true;
        dialogueStep = 0;
        congratsUntil = Time.unscaledTime + congratsSeconds;
        Stage4TestPlayer.InputLocked = true; // frees the mouse for the Restart button
        PlayCongratsJingle();
        StartCoroutine(DialogueSequenceRoutine());
    }

    private float autoExitCountdown = -1f;

    private void HandleStageReset()
    {
        if (stateSequenceCoroutine != null)
        {
            StopCoroutine(stateSequenceCoroutine);
            stateSequenceCoroutine = null;
        }
        showEscapeScreen = false;
        showGameOver = false;
        gameOverExited = false;
        congratsUntil = 0f;
        dialogueStep = 0;
        autoExitCountdown = -1f;
        Stage4TestPlayer.InputLocked = false;
        briefingShown = false;
        briefingUntil = 0f;
        currentInstruction = "";
        instructionUntil = 0f;
        objectiveUntil = 0f;
        cipherActiveDialogue = "";
        cipherDialogueUntil = 0f;
        HideMessage();
    }

    private IEnumerator DialogueSequenceRoutine()
    {
        // The Cipher ending starts after the congratulations screen.
        yield return new WaitForSecondsRealtime(congratsSeconds);

        for (int i = 0; i < CipherDialogueLines.Length; i++)
        {
            dialogueStep = i;
            yield return new WaitForSecondsRealtime(2.5f);
        }

        // All Cipher dialogue lines are complete. Start countdown and auto-exit
        autoExitCountdown = 5.0f;
        while (autoExitCountdown > 0f)
        {
            yield return new WaitForSecondsRealtime(0.2f);
            autoExitCountdown -= 0.2f;
        }

        QuitApplication();
    }

    public void QuitApplication()
    {
        Debug.Log("[Stage4UI] Final level completed and all messages displayed. Exiting game.");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // =====================================================================
    // PUBLIC METHODS (spec section 39)
    // =====================================================================

    public void ShowMessage(string message)
    {
        bannerMessage = message;
        bannerColor = ColorNormal;
        bannerUntil = Time.unscaledTime + 3f;
    }

    public void ShowWarning(string message)
    {
        bannerMessage = message;
        bannerColor = ColorWarning;
        bannerUntil = Time.unscaledTime + 3.5f;
    }

    public void ShowSuccess(string message)
    {
        ShowSuccess(message, 3f);
    }

    public void ShowSuccess(string message, float seconds)
    {
        bannerMessage = message;
        bannerColor = ColorSuccess;
        bannerUntil = Time.unscaledTime + seconds;
    }

    // Banner after a puzzle: tells the player which keypad digit that puzzle gave, and its value.
    private void ShowDigitBanner(string fallback, string digitText)
    {
        if (showDigitReminders && !string.IsNullOrEmpty(digitText))
            ShowSuccess(digitText, digitReminderSeconds);
        else
            ShowSuccess(fallback);
    }

    private string DigitTextAfterPattern()
    {
        Stage4PuzzleGenerator g = manager != null ? manager.Generator : null;
        if (g == null) return "";
        return "Pattern solved! Your answer " + g.GetPatternAnswerValue() + " is DIGIT 1 of the keypad code. Remember it.";
    }

    private string DigitTextAfterMemory()
    {
        Stage4PuzzleGenerator g = manager != null ? manager.Generator : null;
        if (g == null) return "";
        int[] mem = g.GetMemorySequence();
        int last = mem[mem.Length - 1];
        return "Memory verified! The LAST light was " + Stage4PuzzleGenerator.ColorNames[last] + " = " + (last + 1) +
               ", so DIGIT 2 is " + (last + 1) + ". Remember the FIRST light too.";
    }

    private string DigitTextAfterPanel()
    {
        Stage4PuzzleGenerator g = manager != null ? manager.Generator : null;
        if (g == null) return "";
        int panel = g.GetCorrectControlPanel();
        return "Console authorized! Panel " + (char)('A' + panel) + " = number " + (panel + 1) +
               ", so DIGIT 3 is " + (panel + 1) + ". Remember it.";
    }

    private string DigitTextAfterWires()
    {
        Stage4PuzzleGenerator g = manager != null ? manager.Generator : null;
        if (g == null) return "";
        int[] order = g.GetWireOrder();
        int lastTerminal = order[order.Length - 1] + 1;
        return "Power restored! The LAST terminal you connected was number " + lastTerminal +
               ", so DIGIT 4 is " + lastTerminal + ".";
    }

    public void ShowObjective(string message)
    {
        ShowObjective(message, objectiveDisplaySeconds);
    }

    public void ShowObjective(string message, float seconds)
    {
        currentObjective = message;
        objectiveUntil = Time.unscaledTime + seconds;
    }

    public void HideMessage()
    {
        bannerMessage = "";
        bannerUntil = 0f;
    }

    // =====================================================================
    // GUI RENDERING
    // =====================================================================

    private void EnsureStyles()
    {
        if (headerStyle != null) return;

        headerStyle = new GUIStyle(GUI.skin.label);
        headerStyle.fontSize = 15;
        headerStyle.fontStyle = FontStyle.Bold;
        headerStyle.normal.textColor = Color.white;

        objStyle = new GUIStyle(GUI.skin.box);
        objStyle.fontSize = 14;
        objStyle.fontStyle = FontStyle.Bold;
        objStyle.alignment = TextAnchor.MiddleLeft;
        objStyle.padding = new RectOffset(12, 10, 6, 6);
        objStyle.normal.textColor = Color.white;

        timerStyle = new GUIStyle(GUI.skin.box);
        timerStyle.fontSize = 20;
        timerStyle.fontStyle = FontStyle.Bold;
        timerStyle.alignment = TextAnchor.MiddleCenter;
        timerStyle.normal.textColor = Color.white;

        bannerStyle = new GUIStyle(GUI.skin.box);
        bannerStyle.fontSize = 18;
        bannerStyle.fontStyle = FontStyle.Bold;
        bannerStyle.alignment = TextAnchor.MiddleCenter;
        bannerStyle.normal.textColor = Color.white;

        escapeTitleStyle = new GUIStyle(GUI.skin.label);
        escapeTitleStyle.fontSize = 36;
        escapeTitleStyle.fontStyle = FontStyle.Bold;
        escapeTitleStyle.alignment = TextAnchor.MiddleCenter;
        escapeTitleStyle.normal.textColor = Color.white;

        escapeSubStyle = new GUIStyle(GUI.skin.label);
        escapeSubStyle.fontSize = 22;
        escapeSubStyle.fontStyle = FontStyle.Bold;
        escapeSubStyle.alignment = TextAnchor.MiddleCenter;
        escapeSubStyle.normal.textColor = Color.white;

        escapeStatStyle = new GUIStyle(GUI.skin.label);
        escapeStatStyle.fontSize = 26;
        escapeStatStyle.fontStyle = FontStyle.Bold;
        escapeStatStyle.alignment = TextAnchor.MiddleCenter;
        escapeStatStyle.normal.textColor = Color.white;

        dialogueStyle = new GUIStyle(GUI.skin.label);
        dialogueStyle.fontSize = 20;
        dialogueStyle.fontStyle = FontStyle.Italic;
        dialogueStyle.alignment = TextAnchor.MiddleLeft;
        dialogueStyle.wordWrap = true;
        dialogueStyle.normal.textColor = Color.white;

        Texture2D darkBg = new Texture2D(1, 1);
        darkBg.SetPixel(0, 0, new Color(0.04f, 0.08f, 0.14f, 0.94f));
        darkBg.Apply();

        instrBoxStyle = new GUIStyle(GUI.skin.box);
        instrBoxStyle.normal.background = darkBg;

        instrStyle = new GUIStyle(GUI.skin.label);
        instrStyle.fontSize = 14;
        instrStyle.wordWrap = true;
        instrStyle.normal.textColor = Color.white;

        briefTitleStyle = new GUIStyle(GUI.skin.label);
        briefTitleStyle.fontSize = 30;
        briefTitleStyle.fontStyle = FontStyle.Bold;
        briefTitleStyle.alignment = TextAnchor.MiddleCenter;
        briefTitleStyle.normal.textColor = Color.white;

        briefBodyStyle = new GUIStyle(GUI.skin.label);
        briefBodyStyle.fontSize = 17;
        briefBodyStyle.wordWrap = true;
        briefBodyStyle.normal.textColor = Color.white;

        briefHintStyle = new GUIStyle(GUI.skin.label);
        briefHintStyle.fontSize = 14;
        briefHintStyle.alignment = TextAnchor.MiddleCenter;
        briefHintStyle.normal.textColor = Color.white;

        tinyHintStyle = new GUIStyle(GUI.skin.label);
        tinyHintStyle.fontSize = 12;
        tinyHintStyle.normal.textColor = Color.white;

        cipherBoxStyle = new GUIStyle(GUI.skin.box);
        cipherBoxStyle.normal.textColor = Color.white;

        cipherNameStyle = new GUIStyle(GUI.skin.label);
        cipherNameStyle.fontSize = 15;
        cipherNameStyle.fontStyle = FontStyle.Bold;
        cipherNameStyle.normal.textColor = Color.white;
    }

    private void OnGUI()
    {
        EnsureStyles();

        if (showGameOver)
        {
            RenderGameOver();
            return;
        }

        if (showEscapeScreen)
        {
            // Draw in front of every other box and hide whatever is behind (e.g. "All stations engaged")
            GUI.depth = -1000;
            Color oldColor = GUI.color;
            GUI.color = new Color(0.02f, 0.04f, 0.08f, 1f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = oldColor;
            
            if (Time.unscaledTime < congratsUntil) RenderCongratulations();
            else RenderEscapeScreen();
            return;
        }

        float marginX = 28f;
        float marginY = 22f;

        bool showObjectiveHUD = Time.unscaledTime < objectiveUntil;

        // Stage Title and Objective Box: shown for objectiveDisplaySeconds (default 4s) after task completion / objective change
        if (showObjectiveHUD)
        {
            // HUD Top Banner
            GUI.Label(new Rect(marginX, marginY, 350f, 22f), "STAGE 4 — FINAL CONTROL ROOM", headerStyle);

            // Objective box (responsive width with comfortable padding from window borders)
            float maxObjW = Mathf.Min(520f, Screen.width * 0.55f);
            GUI.Box(new Rect(marginX, marginY + 24f, maxObjW, 38f), currentObjective, objStyle);
        }

        // Mission briefing (start of the game) or the HOW TO PLAY box
        if (BriefingVisible)
        {
            RenderBriefing();
        }
        else if (InstructionVisible)
        {
            // Always placed below the top area, so it never sits on top of the Cipher message box.
            RenderInstruction(marginX, marginY);
        }
        else if (manager != null && manager.GetCurrentState() != Stage4State.NotStarted)
        {
            float hintY = showObjectiveHUD ? (marginY + 68f) : marginY;
            GUI.Label(new Rect(marginX, hintY, 360f, 20f), "Press H to show the instructions", tinyHintStyle);
        }

        // Timer HUD (Top Right - safe 28px margin from right edge and 22px from top edge)
        if (timerAdapter != null)
        {
            string timeStr = timerAdapter.GetFormattedTime();
            float timerW = 130f;
            float timerH = 40f;
            float timerX = Screen.width - timerW - marginX;
            float timerY = marginY;
            GUI.Box(new Rect(timerX, timerY, timerW, timerH), "TIME " + timeStr, timerStyle);
        }

        // Dynamic Banner Message (Success, Warning, Info)
        if (BannerVisible)
        {
            bannerStyle.normal.textColor = Color.white;
            bannerStyle.wordWrap = true;
            float bw = Mathf.Min(600f, Screen.width - 80f);
            float bh = Mathf.Max(42f, bannerStyle.CalcHeight(new GUIContent(bannerMessage), bw - 24f) + 14f);
            Rect br = new Rect((Screen.width - bw) * 0.5f, marginY + 68f, bw, bh);
            GUI.Box(br, bannerMessage, bannerStyle);
        }

        // Cipher Active In-Game Dialogue / Hint Box
        // If CipherHostUI is active in the scene, it handles Cipher dialogue to avoid duplicate overlapping boxes.
        if (EscapeRoom.UI.CipherHostUI.Instance == null && CipherDialogueVisible)
        {
            RenderCipherDialoguePopup();
        }
    }

    private void RenderBriefing()
    {
        float w = Mathf.Min(780f, Screen.width - 40f);
        float h = 440f;
        Rect r = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

        GUI.Box(r, GUIContent.none, instrBoxStyle);
        GUI.Label(new Rect(r.x, r.y + 14f, w, 40f), "MISSION BRIEFING", briefTitleStyle);
        GUI.Label(new Rect(r.x + 30f, r.y + 64f, w - 60f, h - 120f), BuildBriefingText(), briefBodyStyle);

        int left = Mathf.CeilToInt(briefingUntil - Time.unscaledTime);
        GUI.Label(new Rect(r.x, r.y + h - 38f, w, 26f),
            "Press SPACE or E to start now (" + left + "s)   |   Press H at any time for help", briefHintStyle);
    }

    private void RenderInstruction(float x = 28f, float y = 90f)
    {
        float w = Mathf.Min(370f, Screen.width * 0.45f);
        float textW = w - 24f;
        float textH = instrStyle.CalcHeight(new GUIContent(currentInstruction), textW);
        Rect r = new Rect(x, y, w, textH + 18f);

        GUI.Box(r, GUIContent.none, instrBoxStyle);
        GUI.Label(new Rect(r.x + 12f, r.y + 9f, textW, textH), currentInstruction, instrStyle);
    }

    private void RenderCipherDialoguePopup()
    {
        float popW = 560f;
        float popH = 110f;
        Rect popRect = new Rect(20f, Screen.height - popH - 25f, popW, popH);

        GUI.Box(popRect, GUIContent.none, cipherBoxStyle);

        // Cipher Portrait Thumbnail
        float picW = 70f;
        float picH = 90f;
        Rect picRect = new Rect(popRect.x + 10f, popRect.y + 10f, picW, picH);
        if (cipherPortrait != null)
        {
            GUI.DrawTexture(picRect, cipherPortrait, ScaleMode.ScaleToFit);
        }
        else
        {
            GUI.Box(picRect, "CIPHER");
        }

        // Dialogue text
        float textX = popRect.x + picW + 20f;
        float textW = popW - picW - 30f;
        GUI.Label(new Rect(textX, popRect.y + 10f, textW, 24f), "CIPHER // THE HACKER NO ONE COULD CATCH", cipherNameStyle);
        GUI.Label(new Rect(textX, popRect.y + 36f, textW, 60f), cipherActiveDialogue, dialogueStyle);
    }

    // =====================================================================
    // GAME OVER (time ran out)
    // =====================================================================

    private void StartGameOver()
    {
        if (showGameOver) return;

        showGameOver = true;
        gameOverExited = false;
        gameOverEndsAt = Time.unscaledTime + gameOverSeconds;
        Stage4TestPlayer.InputLocked = true; // stop movement, free the mouse
        PlayGameOverTone();
    }

    private void Update_GameOver()
    {
        if (!showGameOver || gameOverExited) return;
        if (Time.unscaledTime >= gameOverEndsAt) ExitGame();
    }

    private void ExitGame()
    {
        if (gameOverExited) return;
        gameOverExited = true;

        if (integrationAdapter != null) integrationAdapter.NotifyGameOverExit();

        if (quitOnGameOver)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }

    private void RenderGameOver()
    {
        Update_GameOver();

        // Dark red overlay over the whole screen
        Color old = GUI.color;
        GUI.color = new Color(0.25f, 0f, 0f, 0.82f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = old;

        float w = Mathf.Min(760f, Screen.width - 40f);
        float h = 360f;
        Rect r = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

        GUIStyle title = new GUIStyle(GUI.skin.label);
        title.fontSize = 64;
        title.fontStyle = FontStyle.Bold;
        title.alignment = TextAnchor.MiddleCenter;
        title.normal.textColor = Color.white;
        GUI.Label(new Rect(r.x, r.y + 20f, w, 90f), "GAME OVER", title);

        GUIStyle mid = new GUIStyle(GUI.skin.label);
        mid.fontSize = 24;
        mid.fontStyle = FontStyle.Bold;
        mid.alignment = TextAnchor.MiddleCenter;
        mid.normal.textColor = Color.white;
        GUI.Label(new Rect(r.x, r.y + 120f, w, 36f), "TIME'S UP - THE FACILITY STAYED LOCKED", mid);

        GUIStyle body = new GUIStyle(GUI.skin.label);
        body.fontSize = 18;
        body.alignment = TextAnchor.MiddleCenter;
        body.wordWrap = true;
        body.normal.textColor = Color.white;
        GUI.Label(new Rect(r.x + 40f, r.y + 170f, w - 80f, 70f),
            "You ran out of time before escaping the Final Control Room. Better luck next time!", body);

        int left = Mathf.Max(0, Mathf.CeilToInt(gameOverEndsAt - Time.unscaledTime));
        GUIStyle hint = new GUIStyle(GUI.skin.label);
        hint.fontSize = 18;
        hint.alignment = TextAnchor.MiddleCenter;
        hint.normal.textColor = Color.white;
        GUI.Label(new Rect(r.x, r.y + 250f, w, 30f), "Exiting in " + left + "s...", hint);

        if (GUI.Button(new Rect(r.x + w * 0.5f - 90f, r.y + 295f, 180f, 40f), "Exit now"))
        {
            ExitGame();
        }
    }

    private void PlayGameOverTone()
    {
        const int rate = 44100;
        float[] notes = { 392f, 330f, 262f, 196f };
        float noteLen = 0.25f;
        int perNote = Mathf.CeilToInt(noteLen * rate);
        float[] data = new float[perNote * notes.Length];
        for (int n = 0; n < notes.Length; n++)
        {
            for (int i = 0; i < perNote; i++)
            {
                float env = Mathf.Sin((float)i / perNote * Mathf.PI);
                data[n * perNote + i] = Mathf.Sin(2f * Mathf.PI * notes[n] * i / rate) * env * 0.35f;
            }
        }
        AudioClip clip = AudioClip.Create("GameOverTone", data.Length, 1, rate, false);
        clip.SetData(data, 0);
        Vector3 pos = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
        AudioSource.PlayClipAtPoint(clip, pos, 1f);
    }

    // Shown right after the player walks out through the open door.
    private void RenderCongratulations()
    {
        float w = Mathf.Min(780f, Screen.width - 40f);
        float h = 400f;
        Rect r = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

        GUI.Box(r, GUIContent.none);

        GUIStyle big = new GUIStyle(GUI.skin.label);
        big.fontSize = 52;
        big.fontStyle = FontStyle.Bold;
        big.alignment = TextAnchor.MiddleCenter;
        big.normal.textColor = Color.white;
        GUI.Label(new Rect(r.x, r.y + 30f, w, 70f), "CONGRATULATIONS!", big);

        GUIStyle mid = new GUIStyle(GUI.skin.label);
        mid.fontSize = 24;
        mid.fontStyle = FontStyle.Bold;
        mid.alignment = TextAnchor.MiddleCenter;
        mid.normal.textColor = Color.white;
        GUI.Label(new Rect(r.x, r.y + 112f, w, 36f), "YOU ESCAPED THE FINAL CONTROL ROOM", mid);

        GUIStyle body = new GUIStyle(GUI.skin.label);
        body.fontSize = 18;
        body.alignment = TextAnchor.MiddleCenter;
        body.wordWrap = true;
        body.normal.textColor = Color.white;
        GUI.Label(new Rect(r.x + 40f, r.y + 164f, w - 80f, 90f),
            "You cracked the code, rewired the power, survived the security lockdown and walked out through the blast door. Well done, escape artist!",
            body);

        string finalTime = manager != null ? manager.GetFormattedElapsedTime() : "00:00";
        GUI.Label(new Rect(r.x, r.y + 280f, w, 36f), "TOTAL TIME: " + finalTime, escapeStatStyle);
    }

    // A short rising "victory" tune generated in code (no audio file needed).
    private void PlayCongratsJingle()
    {
        const int rate = 44100;
        float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
        float noteLen = 0.18f;
        int perNote = Mathf.CeilToInt(noteLen * rate);
        float[] data = new float[perNote * notes.Length + rate / 4];
        for (int n = 0; n < notes.Length; n++)
        {
            for (int i = 0; i < perNote; i++)
            {
                float env = Mathf.Sin((float)i / perNote * Mathf.PI);
                data[n * perNote + i] = Mathf.Sin(2f * Mathf.PI * notes[n] * i / rate) * env * 0.35f;
            }
        }
        AudioClip clip = AudioClip.Create("CongratsJingle", data.Length, 1, rate, false);
        clip.SetData(data, 0);

        Vector3 pos = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
        AudioSource.PlayClipAtPoint(clip, pos, 1f);
    }

    private void RenderEscapeScreen()
    {
        // Full screen climax overlay
        float w = 740f;
        float h = 520f;
        Rect r = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

        GUI.Box(r, GUIContent.none);

        GUI.Label(new Rect(r.x, r.y + 20f, w, 40f), "FACILITY ACCESS OVERRIDE SUCCESSFUL", escapeSubStyle);
        GUI.Label(new Rect(r.x, r.y + 60f, w, 50f), "ESCAPE COMPLETE", escapeTitleStyle);

        string finalTime = manager != null ? manager.GetFormattedElapsedTime() : "00:00";
        GUI.Label(new Rect(r.x, r.y + 120f, w, 36f), "TOTAL TIME: " + finalTime, escapeStatStyle);

        // Cipher ending dialogue area with character portrait
        Rect dialogueBoxRect = new Rect(r.x + 35f, r.y + 175f, w - 70f, 240f);
        GUI.Box(dialogueBoxRect, GUIContent.none);

        // Portrait
        float portraitW = 140f;
        float portraitH = 180f;
        Rect portraitRect = new Rect(dialogueBoxRect.x + 20f, dialogueBoxRect.y + 25f, portraitW, portraitH);
        if (cipherPortrait != null)
        {
            GUI.DrawTexture(portraitRect, cipherPortrait, ScaleMode.ScaleToFit);
        }
        else
        {
            GUI.Box(portraitRect, "CIPHER");
        }

        // Cipher Heading and Speech
        float contentX = dialogueBoxRect.x + portraitW + 35f;
        float contentW = dialogueBoxRect.width - portraitW - 50f;
        GUI.Label(new Rect(contentX, dialogueBoxRect.y + 20f, contentW, 26f), "CIPHER: THE HACKER NO ONE COULD CATCH", cipherNameStyle);

        string currentLine = (dialogueStep >= 0 && dialogueStep < CipherDialogueLines.Length)
            ? CipherDialogueLines[dialogueStep]
            : "";
        GUI.Label(new Rect(contentX, dialogueBoxRect.y + 60f, contentW, 80f), currentLine, dialogueStyle);

        if (dialogueStep >= CipherDialogueLines.Length - 1)
        {
            GUI.Label(new Rect(contentX, dialogueBoxRect.y + 140f, contentW, 30f), "FACILITY ESCAPED — ALL STAGES CLEARED", escapeSubStyle);
            int secsLeft = Mathf.Max(1, Mathf.CeilToInt(autoExitCountdown));
            GUIStyle autoExitStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(contentX, dialogueBoxRect.y + 172f, contentW, 25f), $"System shutting down. Auto-exiting game in {secsLeft}s...", autoExitStyle);
        }

        float btnW = 160f;
        float btnH = 38f;
        Rect restartBtnRect = new Rect(r.x + w * 0.5f - btnW - 10f, r.y + h - 50f, btnW, btnH);
        Rect quitBtnRect = new Rect(r.x + w * 0.5f + 10f, r.y + h - 50f, btnW, btnH);

        if (GUI.Button(restartBtnRect, "Restart Stage 4"))
        {
            StopAllCoroutines();
            showEscapeScreen = false;
            Stage4TestPlayer.InputLocked = false;
            if (manager != null) manager.RetryStage4();
        }

        GUI.backgroundColor = new Color(0.70f, 0.20f, 0.20f);
        if (GUI.Button(quitBtnRect, "QUIT NOW [Q]"))
        {
            QuitApplication();
        }
        GUI.backgroundColor = Color.white;
    }
}