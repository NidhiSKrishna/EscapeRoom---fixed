using System;
using System.Collections.Generic;
using UnityEngine;

namespace EscapeRoom.UI
{
    /// <summary>
    /// Lightweight screen-space notification banner for player feedback.
    /// Handles item pickup notices, lock feedback, keypad status messages, and puzzle clues.
    /// Operates without requiring complex Canvas prefabs or font dependencies.
    /// </summary>
    [DisallowMultipleComponent]
    public class FeedbackHUD : MonoBehaviour
    {
        private static FeedbackHUD instance;
        public static FeedbackHUD Instance => instance;

        [Header("Banner Settings")]
        [SerializeField] private float displayDuration = 3.5f;
        [SerializeField] private float fadeDuration = 0.5f;

        private string currentMessage = "";
        private float messageTimer = 0f;
        private Color currentMessageColor = Color.white;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
        }

        private void Update()
        {
            if (messageTimer > 0f)
            {
                messageTimer -= Time.deltaTime;
                if (messageTimer <= 0f)
                {
                    currentMessage = "";
                }
            }
        }

        /// <summary>
        /// Displays a message on the screen for the default duration.
        /// </summary>
        public static void ShowMessage(string message, Color? color = null)
        {
            if (instance == null)
            {
                // Auto-create runtime instance if missing
                var go = new GameObject("FeedbackHUD");
                instance = go.AddComponent<FeedbackHUD>();
                if (Application.isPlaying)
                {
                    DontDestroyOnLoad(go);
                }
            }

            instance.currentMessage = message;
            instance.currentMessageColor = color ?? Color.white;
            instance.messageTimer = instance.displayDuration;
        }

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(currentMessage) || messageTimer <= 0f || Event.current.type != EventType.Repaint)
                return;

            float alpha = 1.0f;
            if (messageTimer < fadeDuration)
            {
                alpha = Mathf.Clamp01(messageTimer / fadeDuration);
            }

            GUIContent content = new GUIContent(currentMessage);
            GUIStyle style = new GUIStyle(GUI.skin.box)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(currentMessageColor.r, currentMessageColor.g, currentMessageColor.b, alpha) }
            };

            Vector2 size = style.CalcSize(content);
            size.x = Mathf.Max(size.x + 36f, 260f);
            size.y += 14f;

            // Position centered horizontally, near the top of the screen (Y = 50px)
            float x = (Screen.width - size.x) * 0.5f;
            float y = 50f;

            Color oldColor = GUI.color;
            GUI.color = new Color(0.08f, 0.08f, 0.12f, 0.90f * alpha);
            GUI.Box(new Rect(x, y, size.x, size.y), GUIContent.none);

            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(new Rect(x, y, size.x, size.y), content, style);
            GUI.color = oldColor;
        }
    }
}
