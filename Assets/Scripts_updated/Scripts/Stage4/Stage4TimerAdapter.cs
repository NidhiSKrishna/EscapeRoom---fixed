using System;
using UnityEngine;

/// <summary>
/// Stage 4 - Timer adapter (spec section 40).
///
/// Listens to Stage4Manager (start / stop / penalty / reset) and keeps the stage countdown.
/// When it reaches zero it tells the manager (NotifyTimerExpired).
///
/// STANDALONE MODE (now): this script counts down by itself, so Stage 4 works when the scene
/// is opened directly.
///
/// WHEN THE MAIN PROJECT HAS ITS OWN StageTimer: do NOT add a second global timer. Change only
/// the bodies of StartTimer / StopTimer / GetRemainingTime / ApplyPenalty below so they call the
/// existing StageTimer instead. Nothing else in Stage 4 needs to change.
///
/// The 30 second lockdown countdown is NOT handled here. It is separate (Stage4SecurityLockdown).
/// </summary>
public class Stage4TimerAdapter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Stage4Manager manager;

    [Header("Settings")]
    [Tooltip("Used only if StartTimer is called with a value of 0 or less. Stage 4 default = 8 minutes.")]
    [SerializeField] private float defaultDurationSeconds = 480f;

    public event Action OnExpired;

    private float remaining;
    private bool running;
    private bool expired;

    // =====================================================================
    // UNITY
    // =====================================================================

    private void Awake()
    {
        if (manager == null) manager = GetComponent<Stage4Manager>();
        if (manager == null) manager = FindFirstObjectByType<Stage4Manager>();
        remaining = defaultDurationSeconds;
    }

    private void Start()
    {
        // Safety: if the manager started the stage before this adapter was listening,
        // catch up now so the clock still runs.
        if (manager == null || running || expired) return;

        Stage4State s = manager.GetCurrentState();
        if (s != Stage4State.NotStarted && s != Stage4State.Escaped && s != Stage4State.Failed)
        {
            StartTimer(manager.StageDurationSeconds);
        }
    }

    private void OnEnable()
    {
        if (manager == null) return;
        manager.OnTimerStartRequested += StartTimer;
        manager.OnTimerStopRequested += StopTimer;
        manager.OnTimePenalty += ApplyPenalty;
        manager.OnStageReset += ResetTimer;
    }

    private void OnDisable()
    {
        if (manager == null) return;
        manager.OnTimerStartRequested -= StartTimer;
        manager.OnTimerStopRequested -= StopTimer;
        manager.OnTimePenalty -= ApplyPenalty;
        manager.OnStageReset -= ResetTimer;
    }

    private void Update()
    {
        if (!running) return;

        remaining -= Time.deltaTime;
        if (remaining <= 0f)
        {
            remaining = 0f;
            running = false;
            expired = true;

            if (OnExpired != null) OnExpired();
            if (manager != null) manager.NotifyTimerExpired();
        }
    }

    // =====================================================================
    // PUBLIC API (spec section 40)
    // =====================================================================

    public void StartTimer(float duration)
    {
        if (duration <= 0f) duration = defaultDurationSeconds;
        remaining = duration;
        running = true;
        expired = false;
    }

    public void StopTimer()
    {
        running = false; // the remaining time stays visible
    }

    public float GetRemainingTime()
    {
        return Mathf.Max(0f, remaining);
    }

    /// <summary>Remaining time as MM:SS.</summary>
    public string GetFormattedTime()
    {
        int total = Mathf.CeilToInt(GetRemainingTime());
        return (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
    }

    public bool IsExpired()
    {
        return expired;
    }

    // =====================================================================
    // EXTRA
    // =====================================================================

    /// <summary>Wrong answers can cost time. Called by the manager through OnTimePenalty.</summary>
    public void ApplyPenalty(float seconds)
    {
        if (!running || seconds <= 0f) return;
        remaining = Mathf.Max(0f, remaining - seconds);
    }

    private void ResetTimer()
    {
        running = false;
        expired = false;
        remaining = defaultDurationSeconds;
    }

    // Temporary on-screen clock for testing. Shows only while Debug Mode is ON on Stage4Manager.
    // The real timer will be drawn by the Stage 4 UI later.
    private void OnGUI()
    {
        if (manager == null || !manager.DebugMode) return;
        GUI.Box(new Rect(Screen.width - 120, 10, 110, 28), GetFormattedTime());
    }
}
