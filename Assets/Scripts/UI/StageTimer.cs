using System;
using System.Collections;
using UnityEngine;
using EscapeRoom.Core;

namespace EscapeRoom.UI
{
    /// <summary>
    /// Countdown timer for a stage. Starts only when gameplay begins (after briefing/intro).
    /// Displays TIME REMAINING in top-right corner via OnGUI.
    /// Fires events at warning thresholds and on expiry.
    /// Stops on stage completion.
    /// White text default; turns yellow at 2 min, red at 30 sec.
    /// </summary>
    [DisallowMultipleComponent]
    public class StageTimer : MonoBehaviour
    {
        private static StageTimer instance;
        public static StageTimer Instance => instance;

        // ── Inspector ────────────────────────────────────────────────────────
        [Header("Timer Settings")]
        [Tooltip("Total seconds allowed for this stage (default: 180 = 3 minutes).")]
        [SerializeField] private float totalSeconds = 180f;

        [Tooltip("Whether to auto-start when this MonoBehaviour starts (false = wait for StartTimer() call).")]
        [SerializeField] private bool autoStart = false;

        [Header("State")]
        [SerializeField] private bool isRunning    = false;
        [SerializeField] private bool isExpired    = false;
        [SerializeField] private bool isCompleted  = false;
        [SerializeField] private float timeRemaining = 0f;

        [Header("Warning Thresholds (seconds)")]
        [SerializeField] private float warn5Min  = 300f;
        [SerializeField] private float warn2Min  = 120f;
        [SerializeField] private float warn1Min  =  60f;
        [SerializeField] private float warn30Sec =  30f;

        // ── Events ────────────────────────────────────────────────────────────
        public event Action OnTimerExpired;
        public event Action<float> OnWarningThreshold; // float = seconds remaining
        public static event Action OnAnyTimerExpired;
        public static event Action OnAnyTimerCompleted;

        // ── Runtime state ─────────────────────────────────────────────────────
        private bool fired5Min  = false;
        private bool fired2Min  = false;
        private bool fired1Min  = false;
        private bool fired30Sec = false;

        // ── Styles ────────────────────────────────────────────────────────────
        private GUIStyle timerStyle;
        private GUIStyle labelStyle;
        private bool stylesBuilt = false;

        // ── Public API ────────────────────────────────────────────────────────
        public float TimeRemaining => timeRemaining;
        public float TotalSeconds  => totalSeconds;
        public bool  IsRunning     => isRunning;
        public bool  IsExpired     => isExpired;
        public bool  IsCompleted   => isCompleted;

        public float NormalizedTimeRemaining =>
            totalSeconds > 0f ? Mathf.Clamp01(timeRemaining / totalSeconds) : 0f;

        // ── Lifecycle ─────────────────────────────────────────────────────────
        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }
            instance = this;
            timeRemaining = totalSeconds;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void OnEnable()
        {
            // Stop timer the moment player escapes (fires via EscapeTrigger → GameManager)
            if (GameManager.Instance != null)
                GameManager.Instance.OnEscapeCompleted += HandleEscapeCompleted;
        }

        private void OnDisable()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnEscapeCompleted -= HandleEscapeCompleted;
        }

        private void HandleEscapeCompleted()
        {
            if (!isCompleted) StopTimer();
        }

        private void Start()
        {
            // Re-bind in case GameManager singleton wasn't ready in OnEnable
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnEscapeCompleted -= HandleEscapeCompleted;
                GameManager.Instance.OnEscapeCompleted += HandleEscapeCompleted;
            }
            if (autoStart) StartTimer();
        }


        private void Update()
        {
            if (!isRunning || isExpired || isCompleted) return;

            timeRemaining -= Time.deltaTime;

            CheckWarnings();

            if (timeRemaining <= 0f)
            {
                timeRemaining = 0f;
                Expire();
            }
        }

        public void ApplyPenalty(float seconds)
        {
            if (!isRunning || isExpired || isCompleted) return;
            timeRemaining = Mathf.Max(0f, timeRemaining - seconds);
            Debug.Log($"<color=#d9534f><b>[StageTimer]</b></color> Time Penalty applied: -{seconds}s. Time remaining: {FormatTime(timeRemaining)}");
            FeedbackHUD.ShowMessage($"TIME PENALTY: -{seconds}s", new Color(0.95f, 0.35f, 0.35f));
            if (timeRemaining <= 0f) Expire();
        }

        public void AddBonusTime(float seconds)
        {
            if (!isRunning || isExpired || isCompleted) return;
            timeRemaining += seconds;
            Debug.Log($"<color=#5cb85c><b>[StageTimer]</b></color> Time Bonus added: +{seconds}s. Time remaining: {FormatTime(timeRemaining)}");
            FeedbackHUD.ShowMessage($"TIME BONUS: +{seconds}s", new Color(0.40f, 0.95f, 0.45f));
        }

        // ── Public API ────────────────────────────────────────────────────────
        public void StartTimer()
        {
            if (isRunning || isExpired || isCompleted) return;
            timeRemaining = totalSeconds;
            isRunning = true;
            fired5Min = fired2Min = fired1Min = fired30Sec = false;
            Debug.Log($"<color=#337ab7><b>[StageTimer]</b></color> Timer started: {FormatTime(timeRemaining)} remaining.");
        }

        /// <summary>
        /// Initiates an emergency countdown clamp (e.g. 1 minute / 60 seconds on door code reveal).
        /// </summary>
        public void TriggerEmergencyCountdown(float seconds)
        {
            if (isCompleted || isExpired) return;
            timeRemaining = Mathf.Min(timeRemaining, seconds);
            if (timeRemaining <= 0f) timeRemaining = seconds;
            isRunning = true;
            fired5Min = true;
            fired2Min = true;
            fired1Min = (timeRemaining <= 60f);
            fired30Sec = (timeRemaining <= 30f);
            Debug.Log($"<color=#d9534f><b>[StageTimer]</b></color> Emergency Countdown triggered: {FormatTime(timeRemaining)} remaining!");
            FeedbackHUD.ShowMessage($"EMERGENCY LOCKDOWN: {FormatTime(timeRemaining)} REMAINING!", new Color(1f, 0.25f, 0.25f));
        }

        public void StopTimer()
        {
            isRunning = false;
            isCompleted = true;
            Debug.Log($"[StageTimer] Timer stopped. Time remaining: {FormatTime(timeRemaining)}");
            OnAnyTimerCompleted?.Invoke();
        }

        public void ResetTimer()
        {
            isRunning    = false;
            isExpired    = false;
            isCompleted  = false;
            timeRemaining = totalSeconds;
            fired5Min = fired2Min = fired1Min = fired30Sec = false;
        }

        // ── Warnings ─────────────────────────────────────────────────────────
        private void CheckWarnings()
        {
            if (!fired5Min  && timeRemaining <= warn5Min  && timeRemaining > warn2Min)  { fired5Min  = true; FireWarning(warn5Min);  }
            if (!fired2Min  && timeRemaining <= warn2Min  && timeRemaining > warn1Min)  { fired2Min  = true; FireWarning(warn2Min);  }
            if (!fired1Min  && timeRemaining <= warn1Min  && timeRemaining > warn30Sec) { fired1Min  = true; FireWarning(warn1Min);  }
            if (!fired30Sec && timeRemaining <= warn30Sec)                              { fired30Sec = true; FireWarning(warn30Sec); }
        }

        private void FireWarning(float threshold)
        {
            OnWarningThreshold?.Invoke(threshold);
            Debug.Log($"[StageTimer] Warning threshold: {FormatTime(threshold)} remaining.");
        }

        private void Expire()
        {
            isRunning = false;
            isExpired = true;
            Debug.Log("<color=#d9534f><b>[StageTimer]</b></color> Timer expired!");
            OnTimerExpired?.Invoke();
            OnAnyTimerExpired?.Invoke();
        }

        // ── Formatting ────────────────────────────────────────────────────────
        public static string FormatTime(float seconds)
        {
            int m = Mathf.FloorToInt(seconds / 60f);
            int s = Mathf.FloorToInt(seconds % 60f);
            return $"{m:00}:{s:00}";
        }

        // ── OnGUI ─────────────────────────────────────────────────────────────
        private void OnGUI()
        {
            if (isCompleted && !isExpired) return; // hide on success
            if (!isRunning && !isExpired) return;

            // Suppress during briefing/intro/pause
            if (AtticBriefingUI.IsBriefingOpen) return;
            if (PhoneIntroUI.IsIntroOpen)        return;
            if (PauseManager.Instance != null && PauseManager.Instance.IsPaused) return;
            if (EscapeUI.Instance != null && EscapeUI.Instance.IsOpen) return;

            if (!stylesBuilt) BuildStyles();

            float sw = Screen.width;
            Color old = GUI.color;

            // Determine color based on time left
            Color timeColor;
            if      (timeRemaining <= 30f)  timeColor = new Color(0.95f, 0.30f, 0.30f);
            else if (timeRemaining <= 120f) timeColor = new Color(0.95f, 0.85f, 0.25f);
            else                            timeColor = Color.white;

            // Timer panel — top-right
            float panelW = 130f, panelH = 52f;
            float panelX = sw - panelW - 12f;
            float panelY = 10f;

            // Dark backing
            GUI.color = new Color(0.05f, 0.06f, 0.08f, 0.78f);
            GUI.DrawTexture(new Rect(panelX, panelY, panelW, panelH), Texture2D.whiteTexture);

            // Accent line (left side)
            GUI.color = timeColor * new Color(1, 1, 1, 0.85f);
            GUI.DrawTexture(new Rect(panelX, panelY, 3f, panelH), Texture2D.whiteTexture);

            GUI.color = Color.white;

            // "TIME" label
            labelStyle.normal.textColor = new Color(0.65f, 0.70f, 0.75f);
            GUI.Label(new Rect(panelX + 10, panelY + 4, panelW - 14, 18), "TIME REMAINING", labelStyle);

            // Time value
            timerStyle.normal.textColor = timeColor;
            GUI.Label(new Rect(panelX + 10, panelY + 20, panelW - 14, 28),
                      isExpired ? "00:00" : FormatTime(timeRemaining), timerStyle);

            GUI.color = old;
        }

        private void BuildStyles()
        {
            timerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 9,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft
            };
            stylesBuilt = true;
        }
    }
}
