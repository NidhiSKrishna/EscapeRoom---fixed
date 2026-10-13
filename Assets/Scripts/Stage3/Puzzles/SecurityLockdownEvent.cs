using System;
using System.Collections;
using UnityEngine;
using EscapeRoom.Interaction;
using EscapeRoom.Core;
using EscapeRoom.UI;
using EscapeRoom.Player;

namespace EscapeRoom.Stage3.Puzzles
{
    /// <summary>
    /// Post-puzzle adrenaline event: SECURITY LOCKDOWN.
    /// After the main memory puzzle is solved, a 15-second countdown begins.
    /// The player must reach and interact with a control panel to deactivate the lockdown.
    /// </summary>
    [DisallowMultipleComponent]
    public class SecurityLockdownEvent : MonoBehaviour, IInteractable
    {
        private static SecurityLockdownEvent instance;
        public static SecurityLockdownEvent Instance => instance;

        [Header("Lockdown Configuration")]
        [Tooltip("Countdown duration in seconds.")]
        [SerializeField] private float countdownDuration = 15f;

        [Tooltip("Time penalty if lockdown timer expires.")]
        [SerializeField] private float failurePenalty = 20f;

        [Header("State")]
        [SerializeField] private bool isLockdownActive = false;
        [SerializeField] private bool isDeactivated = false;
        [SerializeField] private float countdownTimer = 0f;

        // Visual flash state
        private float alarmFlashTimer = 0f;
        private bool alarmFlashOn = false;

        public event Action OnLockdownDeactivated;
        public event Action OnLockdownFailed;

        public bool IsLockdownActive => isLockdownActive;
        public bool IsDeactivated => isDeactivated;
        public float CountdownTimer => countdownTimer;

        // IInteractable
        public string InteractionPrompt => isLockdownActive && !isDeactivated
            ? "Press E to DEACTIVATE LOCKDOWN"
            : (isDeactivated ? "Control Panel (Lockdown Cleared)" : "Emergency Control Panel");
        public bool CanInteract => isLockdownActive && !isDeactivated;

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

        private void Update()
        {
            if (!isLockdownActive || isDeactivated) return;

            // Don't count down while paused
            if (PauseManager.Instance != null && PauseManager.Instance.IsPaused) return;

            countdownTimer -= Time.deltaTime;
            alarmFlashTimer -= Time.deltaTime;

            // Play alarm sound periodically
            if (alarmFlashTimer <= 0f)
            {
                alarmFlashTimer = 0.8f;
                alarmFlashOn = !alarmFlashOn;

                if (Stage3AudioManager.Instance != null)
                {
                    Stage3AudioManager.Instance.PlayAlarmSound();
                }
            }

            // Play countdown tick every second
            float prevSecond = Mathf.Floor(countdownTimer + Time.deltaTime);
            float currSecond = Mathf.Floor(countdownTimer);
            if (currSecond < prevSecond && countdownTimer > 0f)
            {
                if (Stage3AudioManager.Instance != null)
                {
                    Stage3AudioManager.Instance.PlayCountdownTick();
                }
            }

            if (countdownTimer <= 0f)
            {
                HandleLockdownExpired();
            }
        }

        /// <summary>
        /// Activates the security lockdown event.
        /// </summary>
        public void TriggerLockdown()
        {
            if (isLockdownActive || isDeactivated) return;

            isLockdownActive = true;
            countdownTimer = countdownDuration;
            alarmFlashTimer = 0f;
            alarmFlashOn = false;

            Debug.Log($"<color=#d9534f><b>[SecurityLockdownEvent]</b></color> SECURITY LOCKDOWN ACTIVATED! {countdownDuration}s to deactivate!");
            FeedbackHUD.ShowMessage("⚠ SECURITY LOCKDOWN! Reach the control panel!", new Color(0.95f, 0.35f, 0.35f));

            if (BannerUI.Instance != null)
            {
                BannerUI.Instance.ShowBanner("SECURITY LOCKDOWN", "Reach the emergency control panel NOW!", 0.3f, 2.0f, 0.5f);
            }
        }

        public void Interact()
        {
            if (!isLockdownActive || isDeactivated) return;

            DeactivateLockdown();
        }

        private void DeactivateLockdown()
        {
            isDeactivated = true;
            isLockdownActive = false;

            Debug.Log($"<color=#5cb85c><b>[SecurityLockdownEvent]</b></color> Lockdown DEACTIVATED with {countdownTimer:F1}s remaining!");
            FeedbackHUD.ShowMessage("LOCKDOWN DEACTIVATED! Systems restored.", new Color(0.40f, 0.95f, 0.45f));

            if (Stage3AudioManager.Instance != null)
            {
                Stage3AudioManager.Instance.PlayCompletionFanfare();
            }

            ObjectiveManager.Instance?.CompleteFlag("LOCKDOWN_DEACTIVATED");
            OnLockdownDeactivated?.Invoke();
        }

        private void HandleLockdownExpired()
        {
            countdownTimer = 0f;
            isLockdownActive = false;

            Debug.Log("<color=#d9534f><b>[SecurityLockdownEvent]</b></color> Lockdown timer EXPIRED! Applying penalty and resetting.");
            FeedbackHUD.ShowMessage("LOCKDOWN FAILED! Time penalty applied. Try again.", new Color(0.95f, 0.35f, 0.35f));

            if (Stage3AudioManager.Instance != null)
            {
                Stage3AudioManager.Instance.PlayErrorSound();
            }

            if (Stage3Timer.Instance != null)
            {
                Stage3Timer.Instance.ApplyPenalty(failurePenalty);
            }

            OnLockdownFailed?.Invoke();

            // Reset for retry after a brief delay
            StartCoroutine(RetryLockdownAfterDelay(2.5f));
        }

        private IEnumerator RetryLockdownAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);

            // Re-trigger lockdown for retry
            isLockdownActive = false;
            isDeactivated = false;
            TriggerLockdown();
        }

        private void OnGUI()
        {
            if (!isLockdownActive || isDeactivated) return;

            Color oldColor = GUI.color;

            // Red screen flash overlay
            float flashAlpha = alarmFlashOn ? 0.15f : 0.05f;
            GUI.color = new Color(1f, 0f, 0f, flashAlpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

            // Countdown display - large centered text
            float countdownBoxW = 400f;
            float countdownBoxH = 120f;
            float cbX = (Screen.width - countdownBoxW) * 0.5f;
            float cbY = Screen.height * 0.25f;

            // Background box
            GUI.color = new Color(0.08f, 0.02f, 0.02f, 0.90f);
            GUI.Box(new Rect(cbX, cbY, countdownBoxW, countdownBoxH), GUIContent.none);

            // Red border
            GUI.color = new Color(0.95f, 0.15f, 0.15f, 0.90f);
            GUI.Box(new Rect(cbX + 2, cbY + 2, countdownBoxW - 4, countdownBoxH - 4), GUIContent.none);

            // Inner fill
            GUI.color = new Color(0.12f, 0.03f, 0.03f, 0.95f);
            GUI.Box(new Rect(cbX + 4, cbY + 4, countdownBoxW - 8, countdownBoxH - 8), GUIContent.none);

            GUI.color = Color.white;

            // "SECURITY LOCKDOWN" title
            var lockdownTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.95f, 0.25f, 0.25f) }
            };
            GUI.Label(new Rect(cbX + 10, cbY + 12, countdownBoxW - 20, 25), "⚠ SECURITY LOCKDOWN ⚠", lockdownTitleStyle);

            // Timer display
            float displayTime = Mathf.Max(0f, countdownTimer);
            float pulseAlpha = countdownTimer < 5f ? (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f)) : 1f;

            var timerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 42,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.3f, 0.2f, pulseAlpha) }
            };
            GUI.Label(new Rect(cbX + 10, cbY + 40, countdownBoxW - 20, 50), displayTime.ToString("F1") + "s", timerStyle);

            // Instruction
            var instrStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.95f, 0.80f, 0.20f) }
            };
            GUI.Label(new Rect(cbX + 10, cbY + countdownBoxH - 30, countdownBoxW - 20, 20),
                "REACH THE CONTROL PANEL AND PRESS [E]", instrStyle);

            GUI.color = oldColor;
        }
    }
}
