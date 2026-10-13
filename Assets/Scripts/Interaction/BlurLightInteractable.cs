using System.Collections;
using UnityEngine;
using EscapeRoom.Core;

namespace EscapeRoom.Interaction
{
    /// <summary>
    /// Interactive Blur Light anomaly object in the game world.
    /// Highlights on player approach and displays a contextual prompt (e.g. "[F] Interact with Blur Light").
    /// Triggers an ethereal light pulse, audio chime, and Cipher dialogue or hidden item reveal when activated.
    /// Implements IInteractable.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class BlurLightInteractable : MonoBehaviour, IInteractable
    {
        [Header("Appearance & Light")]
        [SerializeField] private Light targetLight;
        [SerializeField] private Renderer lightRenderer;
        [SerializeField] private Color normalColor = new Color(0.20f, 0.70f, 1.0f, 0.8f);
        [SerializeField] private Color highlightColor = new Color(0.40f, 0.95f, 1.0f, 1.0f);
        [SerializeField] private float pulseSpeed = 2.5f;

        [Header("Interaction Settings")]
        [SerializeField] private string promptAction = "Interact with Blur Light";
        [SerializeField] private bool canInteractMultipleTimes = true;

        [Header("Reward / Reaction")]
        [TextArea(2, 4)]
        [SerializeField] private string cipherMessageOnInteract =
            "\"The Blur Light is pulsing with encrypted facility signals. Check the area for hidden energy signatures.\"";

        private bool isHighlighted = false;
        private bool isInteracting = false;
        private Material cachedMaterial;
        private Color originalMaterialColor;

        public string InteractionPrompt => InputConfig.FormatPrompt(promptAction);
        public bool CanInteract => !isInteracting && (canInteractMultipleTimes || !isHighlighted);

        private void Awake()
        {
            if (targetLight == null) targetLight = GetComponentInChildren<Light>();
            if (lightRenderer == null) lightRenderer = GetComponent<Renderer>();

            if (lightRenderer != null)
            {
                cachedMaterial = lightRenderer.material;
                if (cachedMaterial != null && cachedMaterial.HasProperty("_Color"))
                {
                    originalMaterialColor = cachedMaterial.color;
                }
            }

            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = false;
        }

        private void Update()
        {
            // Subtle glowing pulse
            if (targetLight != null)
            {
                float pulse = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
                targetLight.intensity = Mathf.Lerp(1.2f, 2.5f, pulse);
                targetLight.color = Color.Lerp(normalColor, highlightColor, pulse);
            }
        }

        public void SetHighlight(bool highlighted)
        {
            isHighlighted = highlighted;
            if (cachedMaterial != null && cachedMaterial.HasProperty("_Color"))
            {
                cachedMaterial.color = highlighted ? highlightColor : originalMaterialColor;
            }
        }

        public void Interact()
        {
            if (isInteracting) return;
            StartCoroutine(PerformBlurLightPulse());
        }

        private IEnumerator PerformBlurLightPulse()
        {
            isInteracting = true;
            Debug.Log($"<color=#337ab7><b>[BlurLightInteractable]</b></color> Player interacted with Blur Light '{gameObject.name}'.");

            // Play audio chime
            EscapeRoom.Audio.EscapeRoomAudio.PlayAt(
                EscapeRoom.Audio.EscapeRoomAudio.SoundId.CodeNoteReveal,
                transform.position);

            // Flash light brightly
            if (targetLight != null)
            {
                float origIntensity = targetLight.intensity;
                targetLight.intensity = 6.0f;
                yield return new WaitForSeconds(0.25f);
                targetLight.intensity = origIntensity;
            }

            // Display Cipher dialogue
            if (EscapeRoom.UI.CipherHostUI.Instance != null)
            {
                EscapeRoom.UI.CipherHostUI.Instance.ShowMessage(cipherMessageOnInteract, duration: 6.0f);
            }

            EscapeRoom.UI.FeedbackHUD.ShowMessage("Blur Light Activated  ·  Energy Anomaly Detected", new Color(0.25f, 0.90f, 1.0f));

            yield return new WaitForSeconds(1.0f);
            isInteracting = false;
        }
    }
}
