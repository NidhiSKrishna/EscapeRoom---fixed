using UnityEngine;
using UnityEngine.InputSystem;
using EscapeRoom.Core;
using EscapeRoom.Interaction;
using EscapeRoom.Player;

namespace EscapeRoom.UI
{
    /// <summary>
    /// Displays the compact top HUD ('STEP X / 7') with orange flash animations and audio chimes on advancement.
    /// Provides expanded objective inspection panel toggled via T key.
    /// Modal-aware to prevent interference with keypad input or clue reading.
    /// </summary>
    [DisallowMultipleComponent]
    public class ObjectiveHUD : MonoBehaviour
    {
        private static ObjectiveHUD instance;
        public static ObjectiveHUD Instance => instance;

        [Header("Expanded View State")]
        [SerializeField] private bool isExpandedOpen = false;

        [Header("Animation & Polish")]
        [SerializeField] private float flashDuration = 1.0f;
        private float flashTimer = 0f;

        [Header("Audio Chime")]
        [SerializeField] private AudioSource audioSource;
        private AudioClip proceduralDingClip;

        public bool IsExpandedOpen => isExpandedOpen;
        public static bool IsExpandedViewOpen => instance != null && instance.isExpandedOpen;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }
            instance = this;

            EnsureAudioSetup();
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        private void OnEnable()
        {
            if (ObjectiveManager.Instance != null)
            {
                ObjectiveManager.Instance.OnStepChanged += HandleStepChanged;
            }
        }

        private void OnDisable()
        {
            if (ObjectiveManager.Instance != null)
            {
                ObjectiveManager.Instance.OnStepChanged -= HandleStepChanged;
            }
        }

        private void Start()
        {
            if (ObjectiveManager.Instance != null)
            {
                ObjectiveManager.Instance.OnStepChanged -= HandleStepChanged;
                ObjectiveManager.Instance.OnStepChanged += HandleStepChanged;
            }
        }

        private void EnsureAudioSetup()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f; // 2D UI sound
            }

            if (proceduralDingClip == null)
            {
                proceduralDingClip = CreateProceduralDingClip();
            }
        }

        /// <summary>
        /// Generates a pleasant 880Hz sine chime procedural audio clip with zero external asset dependencies.
        /// </summary>
        private AudioClip CreateProceduralDingClip()
        {
            int sampleRate = 44100;
            float duration = 0.25f;
            int numSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[numSamples];
            float frequency = 880.0f; // A5 tone

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Exp(-t * 14.0f); // Exponential decay
                samples[i] = envelope * Mathf.Sin(2.0f * Mathf.PI * frequency * t) * 0.45f;
            }

            AudioClip clip = AudioClip.Create("ObjectiveDing", numSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void HandleStepChanged(int stepIndex, ObjectiveStep step)
        {
            flashTimer = flashDuration;

            if (audioSource != null && proceduralDingClip != null)
            {
                audioSource.PlayOneShot(proceduralDingClip, 0.7f);
            }
        }

        private void Update()
        {
            if (flashTimer > 0f)
            {
                flashTimer -= Time.unscaledDeltaTime;
                if (flashTimer < 0f) flashTimer = 0f;
            }

            HandleInput();
        }

        private void HandleInput()
        {
            // Scene restriction removed: allow objective HUD across stages

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            // Toggle expanded view with T
            if (keyboard.tKey.wasPressedThisFrame)
            {
                if (CanToggleExpandedView())
                {
                    ToggleExpandedView();
                }
            }

            // Close expanded view with ESC
            if (isExpandedOpen && keyboard.escapeKey.wasPressedThisFrame)
            {
                CloseExpandedView();
            }
        }

        public bool CanToggleExpandedView()
        {
            if (KeypadUI.Instance != null && KeypadUI.Instance.IsOpen) return false;
            if (ClueInteractable.IsAnyClueOpen) return false;
            if (EscapeRoom.Interaction.SymbolClueInteractable.IsAnyClueOpen) return false;
            var cabinet = FindAnyObjectByType<EscapeRoom.Puzzle.SymbolCombinationCabinet>();
            if (cabinet != null && cabinet.IsUIOpen) return false;
            if (AtticBriefingUI.IsBriefingOpen) return false;
            if (PhoneIntroUI.IsIntroOpen || EscapeRoom.Stage2.Stage2IntroUI.IsIntroOpen) return false;
            if (PauseManager.Instance != null && PauseManager.Instance.IsPaused) return false;
            if (EscapeUI.Instance != null && EscapeUI.Instance.IsOpen) return false;
            return true;
        }

        public void ToggleExpandedView()
        {
            if (isExpandedOpen)
            {
                CloseExpandedView();
            }
            else
            {
                OpenExpandedView();
            }
        }

        public void OpenExpandedView()
        {
            isExpandedOpen = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            var look = FindAnyObjectByType<PlayerLook>();
            if (look != null) look.SetCursorLock(false);
        }

        public void CloseExpandedView()
        {
            isExpandedOpen = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            var look = FindAnyObjectByType<PlayerLook>();
            if (look != null) look.SetCursorLock(true);
        }

        private void OnGUI()
        {
            // Render HUD across stages where ObjectiveManager is active

            // Do not render HUD when briefing, phone intro, clue, keypad, pause, or escape are open
            if (AtticBriefingUI.IsBriefingOpen || PhoneIntroUI.IsIntroOpen || EscapeRoom.Stage2.Stage2IntroUI.IsIntroOpen) return;
            if (KeypadUI.Instance != null && KeypadUI.Instance.IsOpen) return;
            if (ClueInteractable.IsAnyClueOpen) return;
            // Stage 2 UI guards
            if (EscapeRoom.Interaction.SymbolClueInteractable.IsAnyClueOpen) return;
            var cabinet = FindAnyObjectByType<EscapeRoom.Puzzle.SymbolCombinationCabinet>();
            if (cabinet != null && cabinet.IsUIOpen) return;
            if (PauseManager.Instance != null && PauseManager.Instance.IsPaused) return;
            if (EscapeUI.Instance != null && EscapeUI.Instance.IsOpen) return;

            if (isExpandedOpen)
            {
                DrawExpandedView();
            }
            else
            {
                DrawCompactHUD();
            }
        }

        private void DrawCompactHUD()
        {
            var om = ObjectiveManager.Instance;
            if (om == null || !om.IsSystemActive) return;

            Color oldColor = GUI.color;

            float hudW = Mathf.Min(480f, Screen.width * 0.9f);
            float hudH = 54f;
            float hudX = (Screen.width - hudW) * 0.5f;
            float hudY = 14f;

            // Flash interpolation
            float flashFactor = flashTimer > 0f ? (flashTimer / flashDuration) : 0f;
            Color borderColor = Color.Lerp(new Color(0.25f, 0.30f, 0.38f, 0.85f), new Color(1.0f, 0.55f, 0.10f, 0.95f), flashFactor);
            Color bgColor = Color.Lerp(new Color(0.08f, 0.10f, 0.14f, 0.88f), new Color(0.20f, 0.12f, 0.05f, 0.92f), flashFactor);

            // Background box
            GUI.color = bgColor;
            GUI.Box(new Rect(hudX, hudY, hudW, hudH), GUIContent.none);

            // Border highlight
            GUI.color = borderColor;
            GUI.Box(new Rect(hudX + 2, hudY + 2, hudW - 4, hudH - 4), GUIContent.none);

            // Fill
            GUI.color = bgColor;
            GUI.Box(new Rect(hudX + 4, hudY + 4, hudW - 8, hudH - 8), GUIContent.none);

            GUI.color = Color.white;
            var headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            var instrStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = Color.white }
            };

            if (om.IsFinalEscaped)
            {
                headerStyle.normal.textColor = Color.white;
                GUI.Label(new Rect(hudX + 10, hudY + 6, hudW - 20, 16), "STAGE 3 COMPLETE", headerStyle);
                instrStyle.normal.textColor = Color.white;
                string escapeMsg = !string.IsNullOrEmpty(om.FinalEscapedMessage)
                    ? om.FinalEscapedMessage
                    : "STAGE 3 COMPLETE — FACILITY ESCAPED!";
                GUI.Label(new Rect(hudX + 10, hudY + 24, hudW - 20, 24), escapeMsg, instrStyle);
            }
            else
            {
                int totalSteps = om.TotalSteps;
                int currentStepNum = Mathf.Clamp(om.CurrentStepIndex + 1, 1, Mathf.Max(1, totalSteps));
                string stepInstruction;

                if (om.IsAllCompleted)
                {
                    currentStepNum = totalSteps;
                    stepInstruction = !string.IsNullOrEmpty(om.CompletionInstruction)
                        ? om.CompletionInstruction
                        : "All security systems cleared! Exit unlocked.";
                }
                else
                {
                    stepInstruction = om.CurrentStep != null ? om.CurrentStep.Instruction : "Explore your surroundings.";
                }

                GUI.Label(new Rect(hudX + 10, hudY + 6, hudW - 20, 16), $"STEP {currentStepNum} / {totalSteps}", headerStyle);
                GUI.Label(new Rect(hudX + 10, hudY + 24, hudW - 20, 24), stepInstruction, instrStyle);
            }

            GUI.color = oldColor;
        }

        private void DrawExpandedView()
        {
            var om = ObjectiveManager.Instance;
            if (om == null) return;

            Color oldColor = GUI.color;

            // Full-screen dim
            GUI.color = new Color(0.03f, 0.04f, 0.06f, 0.88f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

            float cardW = Mathf.Min(560f, Screen.width * 0.92f);
            float cardH = Mathf.Min(520f, Screen.height * 0.92f);
            float cardX = (Screen.width - cardW) * 0.5f;
            float cardY = (Screen.height - cardH) * 0.5f;

            // Panel frame
            GUI.color = new Color(0.10f, 0.12f, 0.16f, 0.98f);
            GUI.Box(new Rect(cardX, cardY, cardW, cardH), GUIContent.none);

            // Dark steel cyan border
            GUI.color = new Color(0.24f, 0.30f, 0.40f, 0.85f);
            GUI.Box(new Rect(cardX + 3, cardY + 3, cardW - 6, cardH - 6), GUIContent.none);

            // Background
            GUI.color = new Color(0.09f, 0.11f, 0.15f, 1f);
            GUI.Box(new Rect(cardX + 5, cardY + 5, cardW - 10, cardH - 10), GUIContent.none);

            GUI.color = Color.white;

            // Header Title
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(cardX + 20, cardY + 18, cardW - 40, 32), "ESCAPE ROOM 1", titleStyle);

            // Subtitle
            var subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(cardX + 20, cardY + 50, cardW - 40, 18), "OBJECTIVE PROGRESSION ([T] or [ESC] to close)", subStyle);

            // Divider
            GUI.color = new Color(0.24f, 0.30f, 0.40f, 0.40f);
            GUI.DrawTexture(new Rect(cardX + 30, cardY + 74, cardW - 60, 2), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Steps List
            var steps = om.Steps;
            int currentIdx = om.CurrentStepIndex;

            float listY = cardY + 88f;
            float rowH = 46f;

            if (steps != null)
            {
                for (int i = 0; i < steps.Length; i++)
                {
                    var step = steps[i];
                    if (step == null) continue;

                    string prefix;

                    if (om.IsAllCompleted || i < currentIdx)
                    {
                        prefix = "✓  ";
                    }
                    else if (i == currentIdx)
                    {
                        prefix = "→  ";
                    }
                    else
                    {
                        prefix = "○  ";
                    }

                    var rowStyle = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 13,
                        fontStyle = (i == currentIdx && !om.IsAllCompleted) ? FontStyle.Bold : FontStyle.Normal,
                        alignment = TextAnchor.MiddleLeft,
                        wordWrap = true,
                        normal = { textColor = Color.white }
                    };

                    string rowText = $"{prefix}Step {i + 1}: {step.Instruction}";
                    GUI.Label(new Rect(cardX + 35, listY + (i * rowH), cardW - 70, rowH - 6), rowText, rowStyle);
                }
            }

            // Close Button at bottom
            float btnW = 160f;
            float btnH = 38f;
            float btnX = cardX + (cardW - btnW) * 0.5f;
            float btnY = cardY + cardH - btnH - 18f;

            var closeBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            GUI.backgroundColor = new Color(0.18f, 0.26f, 0.38f, 1f);
            if (GUI.Button(new Rect(btnX, btnY, btnW, btnH), "CLOSE [T]", closeBtnStyle))
            {
                CloseExpandedView();
            }

            GUI.backgroundColor = Color.white;
            GUI.color = oldColor;
        }
    }
}
