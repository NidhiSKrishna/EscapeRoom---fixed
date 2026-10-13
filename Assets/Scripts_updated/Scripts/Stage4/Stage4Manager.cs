using System;
using UnityEngine;

public enum Stage4State
{
    NotStarted,
    PatternPuzzle,
    MemoryPuzzle,
    ControlPanel,
    PathPuzzle,
    CodePuzzle,
    FinalOverride,
    Lockdown,
    Escaped,
    Failed
}

/// <summary>
/// Stage 4 - Final Control Room. Main coordinator.
/// - Owns the stage state machine (no skipping stages).
/// - Generates the puzzle ONCE in StartStage4().
/// - Talks to other scripts ONLY through events, so puzzles, timer, UI, lights
///   and the integration adapter stay decoupled.
/// - Does not load scenes and does not know about any other stage.
/// </summary>
public class Stage4Manager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Stage4PuzzleGenerator generator;

    [Header("Settings")]
    [Tooltip("ON = the stage starts when this scene starts (needed to test the scene directly). " +
             "Turn OFF when the main developer starts it from Stage 3.")]
    [SerializeField] private bool startOnPlay = true;
    [SerializeField] private float stageDurationSeconds = 480f;      // 8 minutes
    [SerializeField] private float wrongAnswerPenaltySeconds = 5f;

    [Header("Debug (development only)")]
    [SerializeField] private bool debugMode = false;

    // ---------------- events (spec section 50 + a few extras) ----------------
    public event Action OnPatternCompleted;
    public event Action OnMemoryCompleted;
    public event Action OnControlPanelCompleted;
    public event Action OnPathCompleted;
    public event Action OnCodeCompleted;
    public event Action OnFinalOverrideBegun;
    public event Action OnLockdownStarted;
    public event Action OnEscapeCompleted;

    public event Action OnStageStarted;                 // puzzle data is ready; puzzles should (re)initialise
    public event Action OnStageReset;                   // puzzles/UI/door/lights should return to default
    public event Action OnStageFailed;
    public event Action<Stage4State> OnStateChanged;

    // Stage4TimerAdapter listens to these three.
    public event Action<float> OnTimerStartRequested;   // seconds
    public event Action OnTimerStopRequested;
    public event Action<float> OnTimePenalty;           // seconds to subtract

    private Stage4State currentState = Stage4State.NotStarted;
    private bool timerExpiredDuringLockdown;
    private float stageStartTime;
    private float stageEndTime;
    private bool clockRunning;
    private float totalPenaltySeconds;

    public Stage4PuzzleGenerator Generator { get { return generator; } }
    public float StageDurationSeconds { get { return stageDurationSeconds; } }
    public bool DebugMode { get { return debugMode; } }

    // =====================================================================
    // UNITY
    // =====================================================================

    private void Awake()
    {
        EnsureGenerator();
    }

    private void Start()
    {
        if (startOnPlay && currentState == Stage4State.NotStarted)
        {
            StartStage4();
        }
    }

    // =====================================================================
    // START / RESET (spec section 9)
    // =====================================================================

    public void StartStage4()
    {
        if (!EnsureGenerator()) return;

        ClearRuntimeState();
        generator.GenerateNewPuzzle();          // generated ONCE per run

        stageStartTime = Time.time;
        clockRunning = true;

        SetState(Stage4State.PatternPuzzle);
        Raise(OnStageStarted, "OnStageStarted");
        Raise(OnTimerStartRequested, stageDurationSeconds, "OnTimerStartRequested");

        if (debugMode) PrintDebugState();
    }

    public void ResetStage4()
    {
        Raise(OnTimerStopRequested, "OnTimerStopRequested");
        if (generator != null) generator.ResetGeneration();

        ClearRuntimeState();
        SetState(Stage4State.NotStarted);
        Raise(OnStageReset, "OnStageReset");
    }

    /// <summary>Reset and start again (used after FailStage4 or a manual retry).</summary>
    public void RetryStage4()
    {
        ResetStage4();
        StartStage4();
    }

    private void ClearRuntimeState()
    {
        clockRunning = false;
        stageStartTime = 0f;
        stageEndTime = 0f;
        totalPenaltySeconds = 0f;
        timerExpiredDuringLockdown = false;
    }

    // =====================================================================
    // PUZZLE PROGRESSION (spec section 9). Wrong-state calls are ignored,
    // so the player can never skip a required stage.
    // =====================================================================

    public void CompletePatternPuzzle()
    {
        if (Advance(Stage4State.PatternPuzzle, Stage4State.MemoryPuzzle, "CompletePatternPuzzle"))
        {
            Raise(OnPatternCompleted, "OnPatternCompleted");
        }
    }

    public void CompleteMemoryPuzzle()
    {
        if (Advance(Stage4State.MemoryPuzzle, Stage4State.ControlPanel, "CompleteMemoryPuzzle"))
        {
            Raise(OnMemoryCompleted, "OnMemoryCompleted");
        }
    }

    public void CompleteControlPanel()
    {
        if (Advance(Stage4State.ControlPanel, Stage4State.PathPuzzle, "CompleteControlPanel"))
        {
            Raise(OnControlPanelCompleted, "OnControlPanelCompleted");
        }
    }

    public void CompletePathPuzzle()
    {
        if (Advance(Stage4State.PathPuzzle, Stage4State.CodePuzzle, "CompletePathPuzzle"))
        {
            Raise(OnPathCompleted, "OnPathCompleted");
        }
    }

    public void CompleteCodePuzzle()
    {
        if (Advance(Stage4State.CodePuzzle, Stage4State.FinalOverride, "CompleteCodePuzzle"))
        {
            Raise(OnCodeCompleted, "OnCodeCompleted");
        }
    }

    /// <summary>Called by the central terminal when the player starts the override sequence.</summary>
    public void BeginFinalOverride()
    {
        if (currentState != Stage4State.FinalOverride)
        {
            Debug.LogWarning("[Stage4] BeginFinalOverride ignored. State is " + currentState + ".");
            return;
        }
        Raise(OnFinalOverrideBegun, "OnFinalOverrideBegun");
    }

    /// <summary>Called when the override's last step finishes. Starts the 30 second event.</summary>
    public void BeginSecurityLockdown()
    {
        if (Advance(Stage4State.FinalOverride, Stage4State.Lockdown, "BeginSecurityLockdown"))
        {
            Raise(OnLockdownStarted, "OnLockdownStarted");
        }
    }

    /// <summary>
    /// Called by the lockdown when its 30 seconds run out. Goes back one step to FinalOverride so the
    /// player can run the override again at the Central Terminal (never an instant fail).
    /// </summary>
    public void RestartFinalOverride()
    {
        if (currentState != Stage4State.Lockdown)
        {
            Debug.LogWarning("[Stage4] RestartFinalOverride ignored. State is " + currentState + ".");
            return;
        }

        if (timerExpiredDuringLockdown)
        {
            // The 8 minutes ran out while the lockdown was running and the lockdown failed too: game over.
            FailStage4();
            return;
        }
        SetState(Stage4State.FinalOverride);
    }

    /// <summary>
    /// Call ONLY after the emergency actions are done and the player passes through the open exit door.
    /// (Stage4ExitDoor is responsible for checking that before calling this.)
    /// </summary>
    public void CompleteEscape()
    {
        if (Advance(Stage4State.Lockdown, Stage4State.Escaped, "CompleteEscape"))
        {
            stageEndTime = Time.time;
            clockRunning = false;
            Raise(OnTimerStopRequested, "OnTimerStopRequested");
            Raise(OnEscapeCompleted, "OnEscapeCompleted");
        }
    }

    /// <summary>
    /// Used only when the stage timer runs out. No instant death: other scripts show a retry option
    /// and call RetryStage4().
    /// </summary>
    public void FailStage4()
    {
        if (currentState == Stage4State.NotStarted ||
            currentState == Stage4State.Escaped ||
            currentState == Stage4State.Failed)
        {
            return;
        }

        clockRunning = false;
        stageEndTime = Time.time;
        SetState(Stage4State.Failed);
        Raise(OnTimerStopRequested, "OnTimerStopRequested");
        Raise(OnStageFailed, "OnStageFailed");
    }

    // =====================================================================
    // TIMER HELPERS
    // =====================================================================

    /// <summary>Stage4TimerAdapter calls this when the 8 minute timer hits zero.</summary>
    public void NotifyTimerExpired()
    {
        // The lockdown has its own separate 30 second countdown, so it decides that outcome.
        // But remember that the main timer already ran out: if the lockdown then fails, the game is over.
        if (currentState == Stage4State.Lockdown)
        {
            timerExpiredDuringLockdown = true;
            return;
        }
        FailStage4();
    }

    /// <summary>Puzzles call this on a wrong answer. Pass a value, or leave empty for the default.</summary>
    public void ApplyTimePenalty(float seconds = -1f)
    {
        float s = seconds < 0f ? wrongAnswerPenaltySeconds : seconds;
        totalPenaltySeconds += s;
        Raise(OnTimePenalty, s, "OnTimePenalty");
    }

    /// <summary>Real seconds played, plus penalties. Stops counting at escape.</summary>
    public float GetElapsedSeconds()
    {
        float elapsed = 0f;
        if (clockRunning) elapsed = Time.time - stageStartTime;
        else if (stageEndTime > stageStartTime) elapsed = stageEndTime - stageStartTime;
        return elapsed + totalPenaltySeconds;
    }

    public string GetFormattedElapsedTime()
    {
        int total = Mathf.Max(0, Mathf.FloorToInt(GetElapsedSeconds()));
        return (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
    }

    // =====================================================================
    // STATE QUERIES
    // =====================================================================

    public bool IsStageComplete()
    {
        return currentState == Stage4State.Escaped;
    }

    public Stage4State GetCurrentState()
    {
        return currentState;
    }

    private bool Advance(Stage4State expected, Stage4State next, string caller)
    {
        if (currentState != expected)
        {
            Debug.LogWarning("[Stage4] " + caller + " ignored. State is " + currentState + ", expected " + expected + ".");
            return false;
        }
        SetState(next);
        return true;
    }

    private void SetState(Stage4State newState)
    {
        currentState = newState;
        if (debugMode) Debug.Log("[Stage4] State -> " + newState);
        Raise(OnStateChanged, newState, "OnStateChanged");
    }

    private bool EnsureGenerator()
    {
        if (generator == null) generator = GetComponent<Stage4PuzzleGenerator>();
        if (generator == null) generator = FindFirstObjectByType<Stage4PuzzleGenerator>();
        if (generator == null)
        {
            Debug.LogError("[Stage4] No Stage4PuzzleGenerator found. Add one to the Stage4Manager object.");
            return false;
        }
        return true;
    }


    // =====================================================================
    // SAFE EVENT RAISING
    // One faulty listener must never stop the other listeners (or the stage) from working.
    // Any error is printed to the Console with the name of the event.
    // =====================================================================

    private void Raise(Action handlers, string eventName)
    {
        if (handlers == null) return;
        foreach (Delegate d in handlers.GetInvocationList())
        {
            try { ((Action)d).Invoke(); }
            catch (Exception e) { Debug.LogError("[Stage4] Error in a listener of " + eventName + ": " + e); }
        }
    }

    private void Raise<T>(Action<T> handlers, T value, string eventName)
    {
        if (handlers == null) return;
        foreach (Delegate d in handlers.GetInvocationList())
        {
            try { ((Action<T>)d).Invoke(value); }
            catch (Exception e) { Debug.LogError("[Stage4] Error in a listener of " + eventName + ": " + e); }
        }
    }

    // =====================================================================
    // DEBUG TOOLS (spec section 56-57). Work only when Debug Mode is ON.
    // =====================================================================

    [ContextMenu("Print Debug State")]
    public void PrintDebugState()
    {
        if (!EnsureGenerator()) return;
        Debug.Log("[Stage4] State: " + currentState + "\n" + generator.GetDebugSummary());
    }

    [ContextMenu("Force Solve Current Puzzle")]
    public void ForceSolveCurrentPuzzle()
    {
        if (!debugMode)
        {
            Debug.LogWarning("[Stage4] Turn on Debug Mode to use ForceSolveCurrentPuzzle.");
            return;
        }

        switch (currentState)
        {
            case Stage4State.PatternPuzzle: CompletePatternPuzzle(); break;
            case Stage4State.MemoryPuzzle: CompleteMemoryPuzzle(); break;
            case Stage4State.ControlPanel: CompleteControlPanel(); break;
            case Stage4State.PathPuzzle: CompletePathPuzzle(); break;
            case Stage4State.CodePuzzle: CompleteCodePuzzle(); break;
            case Stage4State.FinalOverride: BeginSecurityLockdown(); break;
            case Stage4State.Lockdown: CompleteEscape(); break;
            default: break;
        }
    }

    [ContextMenu("Force Complete For Testing")]
    public void ForceCompleteForTesting()
    {
        if (!debugMode)
        {
            Debug.LogWarning("[Stage4] Turn on Debug Mode to use ForceCompleteForTesting.");
            return;
        }

        if (currentState == Stage4State.NotStarted || currentState == Stage4State.Failed) StartStage4();

        for (int i = 0; i < 10 && currentState != Stage4State.Escaped; i++)
        {
            ForceSolveCurrentPuzzle();
        }
    }

    private void OnGUI()
    {
        if (!debugMode || generator == null) return;
        GUI.Box(new Rect(10, 10, 330, 150), "");
        GUI.Label(new Rect(18, 14, 320, 145), "State: " + currentState + "\n" + generator.GetDebugSummary());
    }
}
