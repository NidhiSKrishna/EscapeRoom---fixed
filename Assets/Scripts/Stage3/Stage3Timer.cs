using UnityEngine;
using EscapeRoom.Core;
using EscapeRoom.UI;

namespace EscapeRoom.Stage3
{
    [DisallowMultipleComponent]
    public class Stage3Timer : MonoBehaviour
    {
        public static Stage3Timer Instance { get; private set; }

        [SerializeField]
        [Tooltip("Default timer duration in seconds (480s = 8 minutes)")]
        private float defaultDuration = 480f;

        public float TimeRemaining { get; private set; }
        public bool IsRunning { get; private set; }
        public bool IsExpired { get; private set; }

        public event System.Action OnTimerExpired;

        private GUIStyle timerStyle;
        private Color normalColor = Color.white;
        private Color warningColor = new Color(0.95f, 0.85f, 0.25f);
        private Color dangerColor = new Color(0.95f, 0.35f, 0.35f);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            ResetTimer(defaultDuration);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (!IsRunning || IsExpired)
                return;

            if (PauseManager.Instance != null &&
                PauseManager.Instance.IsPaused)
                return;

            if (GameManager.Instance != null &&
                GameManager.Instance.HasEscaped)
            {
                StopTimer();
                return;
            }

            TimeRemaining -= Time.deltaTime;

            if (TimeRemaining <= 0f)
            {
                TimeRemaining = 0f;
                IsExpired = true;
                IsRunning = false;

                OnTimerExpired?.Invoke();

                FeedbackHUD.ShowMessage(
                    "<color=#f25959>TIME EXPIRED - Security lockout engaged!</color>",
                    dangerColor
                );
            }
        }

        public void StartTimer()
        {
            if (!IsExpired && TimeRemaining > 0f)
            {
                IsRunning = true;
            }
        }

        public void StopTimer()
        {
            IsRunning = false;
        }

        public void ApplyPenalty(float seconds)
        {
            if (IsExpired)
                return;

            TimeRemaining -= seconds;

            if (TimeRemaining < 0f)
                TimeRemaining = 0f;
        }

        public void ResetTimer(float newDuration)
        {
            TimeRemaining = newDuration;
            IsExpired = false;
            IsRunning = false;
        }

        private void OnGUI()
        {
            if (timerStyle == null)
            {
                timerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 32,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.UpperRight
                };
            }

            int minutes = Mathf.FloorToInt(TimeRemaining / 60f);
            int seconds = Mathf.FloorToInt(TimeRemaining % 60f);

            string timeString =
                string.Format("{0:00}:{1:00}", minutes, seconds);

            Color currentColor = normalColor;
            float alpha = 1f;

            if (TimeRemaining <= 60f)
            {
                currentColor = dangerColor;

                if (TimeRemaining < 30f && IsRunning)
                {
                    alpha =
                        0.5f +
                        0.5f * Mathf.Sin(Time.time * 8f);
                }
            }
            else if (TimeRemaining <= 120f)
            {
                currentColor = warningColor;
            }

            currentColor.a = alpha;
            timerStyle.normal.textColor = currentColor;

            // Draw shadow/outline
            float shadowOffset = 2f;

            Color shadowColor =
                new Color(0f, 0f, 0f, alpha * 0.8f);

            GUIStyle shadowStyle =
                new GUIStyle(timerStyle);

            shadowStyle.normal.textColor = shadowColor;

            Rect shadowRect =
                new Rect(
                    Screen.width - 150 + shadowOffset,
                    20 + shadowOffset,
                    130,
                    50
                );

            GUI.Label(
                shadowRect,
                timeString,
                shadowStyle
            );

            // Draw main text
            Rect mainRect =
                new Rect(
                    Screen.width - 150,
                    20,
                    130,
                    50
                );

            GUI.Label(
                mainRect,
                timeString,
                timerStyle
            );
        }
    }
}