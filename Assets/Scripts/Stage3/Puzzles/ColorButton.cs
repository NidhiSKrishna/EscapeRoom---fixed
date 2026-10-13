using System;
using System.Collections;
using UnityEngine;
using EscapeRoom.Interaction;
using EscapeRoom.Core;
using EscapeRoom.UI;

namespace EscapeRoom.Stage3.Puzzles
{
    public enum ColorType
    {
        Red,
        Blue,
        Green,
        Yellow
    }

    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(MeshRenderer))]
    public class ColorButton : MonoBehaviour, IInteractable
    {
        [Header("Button Settings")]
        [SerializeField] private ColorType colorType;

        public bool InputEnabled { get; set; }

        public string InteractionPrompt =>
            $"Press E to press {colorType} button";

        public bool CanInteract => InputEnabled;

        public event Action<ColorButton> OnButtonPressed;

        private MeshRenderer meshRenderer;
        private Material material;
        private Coroutine flashCoroutine;

        public ColorType Type => colorType;

        private void Awake()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            material = meshRenderer.material;

            material.EnableKeyword("_EMISSION");

            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", GetBaseColor());
            }
        }

        public void SetColorType(ColorType newColorType)
        {
            colorType = newColorType;

            if (material == null && meshRenderer != null)
            {
                material = meshRenderer.material;
            }

            if (material != null)
            {
                material.EnableKeyword("_EMISSION");

                if (material.HasProperty("_EmissionColor"))
                {
                    material.SetColor(
                        "_EmissionColor",
                        GetBaseColor()
                    );
                }
            }
        }

        private Color GetBaseColor()
        {
            switch (colorType)
            {
                case ColorType.Red:
                    return new Color(0.8f, 0.05f, 0.05f);

                case ColorType.Blue:
                    return new Color(0.05f, 0.25f, 0.9f);

                case ColorType.Green:
                    return new Color(0.05f, 0.8f, 0.15f);

                case ColorType.Yellow:
                    return new Color(0.9f, 0.75f, 0.05f);

                default:
                    return Color.white;
            }
        }

        public void Interact()
        {
            if (CanInteract)
            {
                OnButtonPressed?.Invoke(this);
            }
        }

        public void PlayFlash()
        {
            if (flashCoroutine != null)
            {
                StopCoroutine(flashCoroutine);
            }

            flashCoroutine = StartCoroutine(
                FlashRoutine(GetBrightColor())
            );
        }

        public void PlayErrorFlash()
        {
            if (flashCoroutine != null)
            {
                StopCoroutine(flashCoroutine);
            }

            flashCoroutine = StartCoroutine(
                FlashRoutine(
                    new Color(1f, 0.2f, 0.2f)
                )
            );
        }

        public void PlayTone()
        {
            if (Stage3AudioManager.Instance != null)
            {
                Stage3AudioManager.Instance.PlayColorTone(
                    (int)colorType
                );
            }
            else
            {
                Debug.Log(
                    "<color=#5cb85c><b>[ColorButton]</b></color> " +
                    "Missing Stage3AudioManager, couldn't play tone."
                );
            }
        }

        public void PlayFlashAndTone()
        {
            PlayFlash();
            PlayTone();
        }

        private IEnumerator FlashRoutine(
            Color flashColor
        )
        {
            float duration = 0.3f;
            float elapsed = 0f;

            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor(
                    "_EmissionColor",
                    flashColor
                );
            }

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;

                Color lerpedColor = Color.Lerp(
                    flashColor,
                    Color.black,
                    elapsed / duration
                );

                if (material.HasProperty("_EmissionColor"))
                {
                    material.SetColor(
                        "_EmissionColor",
                        lerpedColor
                    );
                }

                yield return null;
            }

            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor(
                    "_EmissionColor",
                    Color.black
                );
            }

            flashCoroutine = null;
        }

        private Color GetBrightColor()
        {
            switch (colorType)
            {
                case ColorType.Red:
                    return new Color(1f, 0.2f, 0.2f);

                case ColorType.Blue:
                    return new Color(0.2f, 0.4f, 1f);

                case ColorType.Green:
                    return new Color(0.2f, 1f, 0.3f);

                case ColorType.Yellow:
                    return new Color(1f, 0.9f, 0.2f);

                default:
                    return Color.white;
            }
        }
    }
}