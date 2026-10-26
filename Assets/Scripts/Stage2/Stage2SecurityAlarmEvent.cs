using System;
using UnityEngine;
using EscapeRoom.UI;

namespace EscapeRoom.Stage2
{
    /// <summary>
    /// Manages the 10-second adrenaline security alarm event triggered upon acquiring the keycard.
    /// Pulses emergency lights, plays procedural alarm sirens, displays a high-visibility countdown HUD,
    /// and disarms cleanly when the player swipes the keycard at the exit security reader.
    /// Strictly adheres to the rule: "No instant death. Adrenaline without horror."
    /// </summary>
    [DisallowMultipleComponent]
    public class Stage2SecurityAlarmEvent : MonoBehaviour
    {
        private static Stage2SecurityAlarmEvent instance;
        public static Stage2SecurityAlarmEvent Instance => instance;

        [Header("Alarm Settings")]
        [Tooltip("Emergency countdown duration in seconds.")]
        [SerializeField] private float alarmDuration = 10.0f;
        [SerializeField] private Light[] emergencyLights;

        [Header("State")]
        private bool isAlarmActive = false;
        private bool isDisarmed = false;
        private float remainingTime = 10.0f;
        private bool isOverdue = false;

        public bool IsAlarmActive => isAlarmActive;
        public bool IsDisarmed => isDisarmed;
        public float RemainingTime => remainingTime;

        public static event Action OnAlarmTriggered;
        public static event Action OnAlarmDisarmed;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }
            instance = this;
            remainingTime = alarmDuration;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        public Light[] SirenLights
        {
            get => emergencyLights;
            set => ConfigureEmergencyLights(value);
        }

        public void ConfigureEmergencyLights(Light[] lights)
        {
            emergencyLights = lights;
            if (emergencyLights != null)
            {
                for (int i = 0; i < emergencyLights.Length; i++)
                {
                    if (emergencyLights[i] != null)
                    {
                        emergencyLights[i].enabled = false;
                    }
                }
            }
        }

        /// <summary>
        /// Initiates the security alarm sequence upon picking up the storage keycard.
        /// </summary>
        public void TriggerAlarm()
        {
            if (isAlarmActive || isDisarmed) return;

            isAlarmActive = true;
            isOverdue = false;
            remainingTime = alarmDuration;

            Stage2Audio.Instance?.StartSecurityAlarm();

            if (emergencyLights != null)
            {
                for (int i = 0; i < emergencyLights.Length; i++)
                {
                    if (emergencyLights[i] != null)
                        emergencyLights[i].enabled = true;
                }
            }

            FeedbackHUD.ShowMessage("ALERT: Security sensor tripped! Swipe keycard at the exit reader!", new Color(0.95f, 0.35f, 0.35f));
            OnAlarmTriggered?.Invoke();
            Debug.Log("[Stage2SecurityAlarmEvent] Security alarm activated. 10s countdown started.");
        }

        /// <summary>
        /// Disarms the security alarm when the keycard is swiped at the security reader.
        /// </summary>
        public void DisarmAlarm()
        {
            if (!isAlarmActive && isDisarmed) return;

            isAlarmActive = false;
            isDisarmed = true;

            Stage2Audio.Instance?.StopSecurityAlarm();

            if (emergencyLights != null)
            {
                for (int i = 0; i < emergencyLights.Length; i++)
                {
                    if (emergencyLights[i] != null)
                    {
                        emergencyLights[i].color = new Color(0.25f, 0.90f, 0.40f);
                        emergencyLights[i].intensity = 1.0f;
                    }
                }
            }

            FeedbackHUD.ShowMessage("SECURITY OVERRIDE CONFIRMED. Exit unlocked!", new Color(0.35f, 0.95f, 0.40f));
            OnAlarmDisarmed?.Invoke();
            Debug.Log("[Stage2SecurityAlarmEvent] Security alarm successfully disarmed.");
        }

        private void Update()
        {
            if (!isAlarmActive || isDisarmed) return;

            remainingTime -= Time.deltaTime;

            // Pulse emergency lights
            if (emergencyLights != null)
            {
                float pulse = Mathf.PingPong(Time.time * 3.5f, 1f);
                float intensity = Mathf.Lerp(0.8f, 3.2f, pulse);
                Color lightColor = Color.Lerp(new Color(0.95f, 0.20f, 0.10f), new Color(0.98f, 0.65f, 0.15f), pulse);

                for (int i = 0; i < emergencyLights.Length; i++)
                {
                    if (emergencyLights[i] != null)
                    {
                        emergencyLights[i].intensity = intensity;
                        emergencyLights[i].color = lightColor;
                    }
                }
            }

            if (remainingTime <= 0f && !isOverdue)
            {
                remainingTime = 0f;
                isOverdue = true;
                FeedbackHUD.ShowMessage("WARNING: Lockdown imminent! Reach the exit reader now!", new Color(0.98f, 0.30f, 0.30f));
            }
        }

        private void OnGUI()
        {
            if (!isAlarmActive || isDisarmed) return;

            // Emergency HUD banner at top of screen
            float barW = Mathf.Min(640f, Screen.width * 0.85f);
            float barH = 58f;
            float barX = (Screen.width - barW) * 0.5f;
            float barY = 55f;

            Color oldColor = GUI.color;

            // Flashing border
            float blink = Mathf.PingPong(Time.time * 4f, 1f);
            Color borderColor = Color.Lerp(new Color(0.85f, 0.15f, 0.15f, 0.95f), new Color(0.98f, 0.70f, 0.20f, 0.95f), blink);

            GUI.color = new Color(0.10f, 0.05f, 0.05f, 0.92f);
            GUI.Box(new Rect(barX, barY, barW, barH), GUIContent.none);

            GUI.color = borderColor;
            GUI.Box(new Rect(barX + 2, barY + 2, barW - 4, barH - 4), GUIContent.none);

            // Title (White #FFFFFF)
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUI.color = Color.white;

            string titleText = isOverdue 
                ? "⚠ AUXILIARY LOCKDOWN IN PROGRESS // DISARM AT EXIT READER!" 
                : "⚠ SECURITY ALERT: EVACUATION PROTOCOL ENGAGED";
            GUI.Label(new Rect(barX + 10, barY + 6, barW - 20, 20f), titleText, titleStyle);

            // Countdown Style
            var countStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            string timerString = isOverdue 
                ? "LOCKDOWN OVERDUE — SWIPE KEYCARD AT READER IMMEDIATELY!" 
                : $"REACH EXIT READER IN: {remainingTime:F1}s";
            GUI.Label(new Rect(barX + 10, barY + 28, barW - 20, 24f), timerString, countStyle);

            GUI.color = oldColor;
        }
    }
}
