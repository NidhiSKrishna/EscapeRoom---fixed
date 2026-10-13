using System.Collections;
using UnityEngine;

namespace EscapeRoom.UI
{
    /// <summary>
    /// Displays dramatic room entrance banners with title, subtitle tip, fade-in, hold, and fade-out animations.
    /// Operates via OnGUI for robust, zero-dependency rendering across scene reloads.
    /// </summary>
    [DisallowMultipleComponent]
    public class BannerUI : MonoBehaviour
    {
        private static BannerUI instance;
        public static BannerUI Instance => instance;

        [Header("State")]
        [SerializeField] private string currentTitle = "";
        [SerializeField] private string currentTip = "";
        [SerializeField] private float currentAlpha = 0f;

        private Coroutine bannerCoroutine;

        public bool IsShowing => currentAlpha > 0.01f;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
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

        /// <summary>
        /// Shows a room banner with fade-in, display hold, and fade-out.
        /// </summary>
        public void ShowBanner(string title, string tip, float fadeInDuration = 0.5f, float holdDuration = 3.0f, float fadeOutDuration = 0.8f)
        {
            currentTitle = title;
            currentTip = tip;

            if (bannerCoroutine != null)
            {
                StopCoroutine(bannerCoroutine);
            }
            bannerCoroutine = StartCoroutine(AnimateBanner(fadeInDuration, holdDuration, fadeOutDuration));
        }

        private IEnumerator AnimateBanner(float fadeIn, float hold, float fadeOut)
        {
            // Fade In
            float elapsed = 0f;
            while (elapsed < fadeIn)
            {
                elapsed += Time.unscaledDeltaTime;
                currentAlpha = Mathf.Clamp01(elapsed / fadeIn);
                yield return null;
            }
            currentAlpha = 1f;

            // Hold
            yield return new WaitForSecondsRealtime(hold);

            // Fade Out
            elapsed = 0f;
            while (elapsed < fadeOut)
            {
                elapsed += Time.unscaledDeltaTime;
                currentAlpha = Mathf.Clamp01(1f - (elapsed / fadeOut));
                yield return null;
            }
            currentAlpha = 0f;
            bannerCoroutine = null;
        }

        public void ResetBanner()
        {
            if (bannerCoroutine != null)
            {
                StopCoroutine(bannerCoroutine);
                bannerCoroutine = null;
            }
            currentAlpha = 0f;
            currentTitle = "";
            currentTip = "";
        }

        private void OnGUI()
        {
            if (currentAlpha <= 0.001f) return;

            Color oldColor = GUI.color;

            float bannerW = Mathf.Min(600f, Screen.width * 0.9f);
            float bannerH = 110f;
            float bannerX = (Screen.width - bannerW) * 0.5f;
            float bannerY = 85f; // Placed nicely below the compact ObjectiveHUD

            // Subtle dark backing strip
            GUI.color = new Color(0.04f, 0.06f, 0.09f, 0.80f * currentAlpha);
            GUI.Box(new Rect(bannerX, bannerY, bannerW, bannerH), GUIContent.none);

            // Subtle top and bottom accent lines
            GUI.color = new Color(0.96f, 0.78f, 0.20f, 0.70f * currentAlpha);
            GUI.DrawTexture(new Rect(bannerX + 20, bannerY, bannerW - 40, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(bannerX + 20, bannerY + bannerH - 2, bannerW - 40, 2), Texture2D.whiteTexture);

            // Title Label (Large, bold, white)
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1.0f, 1.0f, 1.0f, currentAlpha) }
            };
            GUI.color = Color.white;
            GUI.Label(new Rect(bannerX + 10, bannerY + 12, bannerW - 20, 40), currentTitle, titleStyle);

            // Tip Label (Smaller, soft text)
            var tipStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1.0f, 1.0f, 1.0f, currentAlpha * 0.9f) }
            };
            GUI.Label(new Rect(bannerX + 15, bannerY + 54, bannerW - 30, 40), currentTip, tipStyle);

            GUI.color = oldColor;
        }
    }
}
