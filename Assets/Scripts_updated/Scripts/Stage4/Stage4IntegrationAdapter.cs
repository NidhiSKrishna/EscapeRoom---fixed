using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>A UnityEvent that carries one text message (used for the Cipher hooks).</summary>
[Serializable]
public class Stage4StringEvent : UnityEvent<string> { }

/// <summary>
/// Stage 4 - Integration adapter (spec sections 43, 48, 49).
///
/// The ONLY script the main developer needs to know about:
///   StartStage(), ResetStage(), IsComplete(), GetCurrentState(),
///   GetRemainingTime(), GetFinalTime(), CompleteEscape(), ForceCompleteForTesting()
///
/// CIPHER: this project's Cipher system is not part of Stage 4. This script only asks for a message.
/// The main developer connects the existing CipherHostUI in the Inspector:
///   "On Cipher Hint" and "On Cipher Dialogue" -> drag CipherHostUI -> pick its method that shows text.
/// Until then a simple fallback box shows the message so the stage can be tested alone.
///
/// FINISHING STAGE 4: connect "On Stage Complete" in the Inspector (or subscribe to OnStageComplete).
/// Stage 4 never loads a scene itself. There is no Stage 5.
/// </summary>
public class Stage4IntegrationAdapter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Stage4Manager manager;
    [SerializeField] private Stage4TimerAdapter timerAdapter;

    [Header("Cipher hooks (main developer connects CipherHostUI here)")]
    [SerializeField] private Stage4StringEvent onCipherHint = new Stage4StringEvent();
    [SerializeField] private Stage4StringEvent onCipherDialogue = new Stage4StringEvent();

    [Header("Stage hooks")]
    [SerializeField] private UnityEvent onStageComplete = new UnityEvent();
    [Tooltip("Fires when the GAME OVER screen ends (time ran out). Main project: load the menu / end the game here.")]
    [SerializeField] private UnityEvent onGameOverExit = new UnityEvent();

    [Header("Fallback message box (testing only)")]
    [SerializeField] private bool showFallbackCipherBox = true;
    [SerializeField] private float fallbackBoxSeconds = 5f;

    public event Action<string> OnCipherHintRequested;
    public event Action<string> OnCipherDialogueRequested;
    public event Action OnStageComplete;
    public event Action OnGameOverExit;

    private string fallbackMessage = "";
    private float fallbackHideTime;
    private int hintLevel;

    // =====================================================================
    // UNITY
    // =====================================================================

    private void Awake()
    {
        if (manager == null) manager = GetComponent<Stage4Manager>();
        if (manager == null) manager = FindFirstObjectByType<Stage4Manager>();
        if (timerAdapter == null) timerAdapter = GetComponent<Stage4TimerAdapter>();
        if (timerAdapter == null) timerAdapter = FindFirstObjectByType<Stage4TimerAdapter>();
    }

    private void OnEnable()
    {
        if (manager == null) return;
        manager.OnEscapeCompleted += HandleEscapeCompleted;
        manager.OnStateChanged += HandleStateChanged;
    }

    private void OnDisable()
    {
        if (manager == null) return;
        manager.OnEscapeCompleted -= HandleEscapeCompleted;
        manager.OnStateChanged -= HandleStateChanged;
    }

    private void HandleEscapeCompleted()
    {
        if (OnStageComplete != null) OnStageComplete();
        if (onStageComplete != null) onStageComplete.Invoke();
    }

    /// <summary>Stage4UI calls this when the GAME OVER screen has finished (before the game exits).</summary>
    public void NotifyGameOverExit()
    {
        if (OnGameOverExit != null) OnGameOverExit();
        if (onGameOverExit != null) onGameOverExit.Invoke();
    }

    private void HandleStateChanged(Stage4State state)
    {
        hintLevel = 0; // a new stage of the puzzle starts with the gentlest hint again
    }

    // =====================================================================
    // PUBLIC INTEGRATION API (spec sections 48 and 49)
    // =====================================================================

    public void StartStage()
    {
        if (manager != null) manager.StartStage4();
    }

    public void ResetStage()
    {
        if (manager != null) manager.ResetStage4();
    }

    public bool IsComplete()
    {
        return manager != null && manager.IsStageComplete();
    }

    public Stage4State GetCurrentState()
    {
        return manager != null ? manager.GetCurrentState() : Stage4State.NotStarted;
    }

    public float GetRemainingTime()
    {
        return timerAdapter != null ? timerAdapter.GetRemainingTime() : 0f;
    }

    /// <summary>Time the player took, as MM:SS (includes time penalties).</summary>
    public string GetFinalTime()
    {
        return manager != null ? manager.GetFormattedElapsedTime() : "00:00";
    }

    /// <summary>Normally called by the exit door once the lockdown is solved.</summary>
    public void CompleteEscape()
    {
        if (manager != null) manager.CompleteEscape();
    }

    /// <summary>Development / testing only. Needs Debug Mode ON on Stage4Manager. Never called automatically.</summary>
    public void ForceCompleteForTesting()
    {
        if (manager != null) manager.ForceCompleteForTesting();
    }

    // =====================================================================
    // CIPHER (spec section 43)
    // =====================================================================

    public void ShowCipherHint(string message)
    {
        if (string.IsNullOrEmpty(message)) return;
        ShowFallback(message);
        if (OnCipherHintRequested != null) OnCipherHintRequested(message);
        if (onCipherHint != null) onCipherHint.Invoke(message);
    }

    public void ShowCipherDialogue(string message)
    {
        if (string.IsNullOrEmpty(message)) return;
        ShowFallback(message);
        if (OnCipherDialogueRequested != null) OnCipherDialogueRequested(message);
        if (onCipherDialogue != null) onCipherDialogue.Invoke(message);
    }

    /// <summary>
    /// Shows a hint that matches the CURRENT stage and the ACTUAL generated puzzle.
    /// Hints never state the answer. Hook this to the "Ask Cipher" key later.
    /// </summary>
    [ContextMenu("Request Hint")]
    public void RequestHint()
    {
        if (manager == null) return;
        ShowCipherHint(BuildHint(manager.GetCurrentState()));
        hintLevel++;
    }

    private string BuildHint(Stage4State state)
    {
        Stage4PuzzleGenerator gen = manager.Generator;

        switch (state)
        {
            case Stage4State.PatternPuzzle:
                string type = gen != null ? gen.GetPatternTypeName() : "";
                if (type == "Increasing") return "Think about the sequence. The numbers keep climbing.";
                if (type == "Decreasing") return "Think about the sequence. The numbers keep falling.";
                if (type == "Alternating") return "Think about the sequence. Two values keep taking turns.";
                if (type == "Cycle") return "Think about the sequence. A small group of numbers repeats.";
                return "Think about the sequence.";

            case Stage4State.MemoryPuzzle:
                return "Remember what the system showed you. The first and the last light both matter.";

            case Stage4State.ControlPanel:
                return "Remember what the system showed you. Which light came first?";

            case Stage4State.PathPuzzle:
                return "Voltage is the clue. Wire the lowest first and work upward. Remember the last terminal.";

            case Stage4State.CodePuzzle:
                if (hintLevel >= 1 && gen != null) return gen.GetCodeRecipe();
                return "The final code is built from what you discovered.";

            case Stage4State.FinalOverride:
                return "The final lock is different.";

            case Stage4State.Lockdown:
                return "Stay calm. Follow the emergency prompts.";

            default:
                return "I am watching the system. Keep going.";
        }
    }

    // =====================================================================
    // FALLBACK BOX
    // =====================================================================

    private void ShowFallback(string message)
    {
        fallbackMessage = message;
        fallbackHideTime = Time.time + fallbackBoxSeconds;
    }

    private void OnGUI()
    {
        if (!showFallbackCipherBox) return;
        if (string.IsNullOrEmpty(fallbackMessage) || Time.time > fallbackHideTime) return;

        GUI.Box(new Rect(10, Screen.height - 90, 520, 80), "CIPHER");
        GUI.Label(new Rect(20, Screen.height - 66, 500, 52), fallbackMessage);
    }
}
