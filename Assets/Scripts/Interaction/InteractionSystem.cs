using System;
using UnityEngine;
using UnityEngine.InputSystem;
using EscapeRoom.UI;

namespace EscapeRoom.Interaction
{
    /// <summary>
    /// Centralized raycast interaction system mounted on the Player.
    /// Casts a ray from the center of the PlayerCamera to detect IInteractable objects.
    /// Handles interact key (E) detection via the Unity Input System and renders a clean screen-space prompt.
    /// </summary>
    [DisallowMultipleComponent]
    public class InteractionSystem : MonoBehaviour
    {
        [Header("Detection Settings")]
        [Tooltip("The camera from which the center-screen interaction ray originates.")]
        [SerializeField] private Camera playerCamera;

        [Tooltip("Maximum interaction reach in meters (default 3.0m).")]
        [SerializeField] private float interactionRange = 3.0f;

        [Tooltip("Layer mask filter for interactable raycasting.")]
        [SerializeField] private LayerMask interactionLayers = ~0;

        [Tooltip("Whether interaction raycasts detect trigger colliders.")]
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

        [Header("Prompt Display")]
        [Tooltip("Whether to draw the minimal screen-space prompt automatically.")]
        [SerializeField] private bool showScreenPrompt = true;

        [Tooltip("Default prompt text if an interactable has an empty prompt string.")]
        [SerializeField] private string defaultPromptText = "Press E to interact";

        [Header("Debugging")]
        [Tooltip("Log interaction raycasting, detection target changes, and E key presses to console.")]
        [SerializeField] private bool showInteractionDebug = false;

        private IInteractable currentInteractable;
        private GameObject currentHitObject;
        private RaycastHit currentHitInfo;

        public event Action<IInteractable> OnInteractableFocused;
        public event Action OnInteractableLost;

        public Camera PlayerCamera
        {
            get => playerCamera;
            set => playerCamera = value;
        }

        public float InteractionRange
        {
            get => interactionRange;
            set => interactionRange = Mathf.Max(0.1f, value);
        }

        public bool ShowInteractionDebug
        {
            get => showInteractionDebug;
            set => showInteractionDebug = value;
        }

        public IInteractable CurrentInteractable => currentInteractable;
        public GameObject CurrentHitObject => currentHitObject;
        public bool HasInteractableTarget => currentInteractable != null && currentInteractable.CanInteract;

        private void Start()
        {
            if (playerCamera == null)
            {
                playerCamera = GetComponentInChildren<Camera>();
                if (playerCamera == null)
                {
                    playerCamera = Camera.main;
                }
            }
        }

        private void Update()
        {
            PerformRaycastDetection();
            HandleInteractionInput();
        }

        private void PerformRaycastDetection()
        {
            if (playerCamera == null) return;

            // Cast ray straight through the center of the player camera viewport
            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            RaycastHit[] hits = Physics.RaycastAll(ray, interactionRange, interactionLayers, triggerInteraction);

            IInteractable foundInteractable = null;
            GameObject hitGo = null;
            RaycastHit selectedHit = default;

            if (hits != null && hits.Length > 0)
            {
                // Sort hits by distance ascending
                Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                Transform playerRoot = transform.root;
                for (int i = 0; i < hits.Length; i++)
                {
                    RaycastHit hit = hits[i];
                    Collider col = hit.collider;
                    if (col == null) continue;

                    // Skip the player's own CharacterController or colliders
                    if (col.transform.root == playerRoot || col.gameObject == gameObject) continue;

                    // Skip trigger colliders if triggerInteraction is Ignore
                    if (triggerInteraction == QueryTriggerInteraction.Ignore && col.isTrigger) continue;

                    // Check for IInteractable directly on this collider
                    IInteractable directInteractable = col.GetComponent<IInteractable>();
                    if (directInteractable != null && directInteractable.CanInteract)
                    {
                        hitGo = col.gameObject;
                        selectedHit = hit;
                        foundInteractable = directInteractable;
                        break;
                    }

                    // Check in parent or immediate children
                    IInteractable hierarchyInteractable = col.GetComponentInParent<IInteractable>()
                                                        ?? col.GetComponentInChildren<IInteractable>();

                    if (hierarchyInteractable != null && hierarchyInteractable.CanInteract)
                    {
                        hitGo = col.gameObject;
                        selectedHit = hit;
                        foundInteractable = hierarchyInteractable;
                        break;
                    }

                    // If this collider belongs to an opened container, don't let its frame block items inside
                    var container = col.GetComponentInParent<LockedContainer>();
                    if (container != null && container.IsOpen)
                    {
                        continue;
                    }

                    // This is the first valid physical blocking surface hit
                    hitGo = col.gameObject;
                    selectedHit = hit;
                    foundInteractable = null;
                    break;
                }
            }

            currentHitInfo = selectedHit;
            currentHitObject = hitGo;

            // Validate interactable state
            if (foundInteractable != null && !foundInteractable.CanInteract)
            {
                foundInteractable = null;
            }

            // Update focused interactable state
            if (foundInteractable != currentInteractable)
            {
                if (showInteractionDebug)
                {
                    string targetName = foundInteractable != null ? foundInteractable.GetType().Name : "None";
                    string hitName = hitGo != null ? hitGo.name : "None";
                    Debug.Log($"[InteractionSystem] Target changed -> {targetName} on '{hitName}' (Dist: {currentHitInfo.distance:F2}m)");
                }

                currentInteractable = foundInteractable;

                if (currentInteractable != null && currentInteractable.CanInteract)
                {
                    OnInteractableFocused?.Invoke(currentInteractable);
                }
                else
                {
                    OnInteractableLost?.Invoke();
                }
            }
        }

        private void HandleInteractionInput()
        {
            bool interactPressed = EscapeRoom.Core.InputConfig.IsInteractPressedThisFrame() ||
                                  EscapeRoom.Core.InputConfig.IsOpenPressedThisFrame() ||
                                  EscapeRoom.Core.InputConfig.IsUsePressedThisFrame();

            if (interactPressed)
            {
                if (showInteractionDebug)
                {
                    string targetName = currentInteractable != null ? currentInteractable.GetType().Name : "None";
                    bool canInteract = currentInteractable != null && currentInteractable.CanInteract;
                    bool suppressed = ShouldSuppressInteraction();
                    Debug.Log($"[InteractionSystem] Interaction key pressed. Target: {targetName}, CanInteract: {canInteract}, Suppressed: {suppressed}");
                }

                if (!ShouldSuppressInteraction())
                {
                    TriggerInteract();
                }
            }
        }

        /// <summary>
        /// Determines whether interaction detection and prompts should be suppressed (e.g. during modals, pause, or win screen).
        /// </summary>
        public bool ShouldSuppressInteraction()
        {
            if (KeypadUI.Instance != null && KeypadUI.Instance.IsOpen) return true;
            if (EscapeRoom.Stage3.Puzzles.SymbolMappingPuzzle.Instance != null && EscapeRoom.Stage3.Puzzles.SymbolMappingPuzzle.Instance.IsUIOpen) return true;
            if (EscapeRoom.Stage3.UI.MonitorDisplayUI.IsAnyMonitorOpen) return true;
            if (ClueInteractable.IsAnyClueOpen) return true;
            // Stage 2 UI suppression
            if (EscapeRoom.Interaction.SymbolClueInteractable.IsAnyClueOpen) return true;
            var cabinet = FindAnyObjectByType<EscapeRoom.Puzzle.SymbolCombinationCabinet>();
            if (cabinet != null && cabinet.IsUIOpen) return true;
            if (EscapeRoom.Core.PauseManager.Instance != null && EscapeRoom.Core.PauseManager.Instance.IsPaused) return true;
            if (EscapeRoom.UI.EscapeUI.Instance != null && EscapeRoom.UI.EscapeUI.Instance.IsOpen) return true;
            if (EscapeRoom.Stage2.Stage2ExitTrigger.IsStage2Complete) return true;
            if (EscapeRoom.Stage2.Stage2IntroUI.IsIntroOpen) return true;
            
            string activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (activeScene == "EscapeRoom_Main" && EscapeRoom.Core.GameManager.Instance != null && EscapeRoom.Core.GameManager.Instance.HasEscaped) return true;
            return false;
        }

        /// <summary>
        /// Attempts interaction with the currently targeted object.
        /// </summary>
        public void TriggerInteract()
        {
            if (ShouldSuppressInteraction()) return;

            if (currentInteractable != null && currentInteractable.CanInteract)
            {
                if (showInteractionDebug)
                {
                    Debug.Log($"[InteractionSystem] Calling Interact() on {currentInteractable.GetType().Name} ({currentHitObject?.name})");
                }
                currentInteractable.Interact();
            }
        }

        private void OnGUI()
        {
            if (!showScreenPrompt || Event.current.type != EventType.Repaint) return;
            if (ShouldSuppressInteraction()) return;

            // Draw subtle center crosshair dot
            DrawCrosshairDot();

            // Draw interaction prompt if looking at a valid interactable
            if (currentInteractable != null && currentInteractable.CanInteract)
            {
                string text = !string.IsNullOrEmpty(currentInteractable.InteractionPrompt)
                    ? currentInteractable.InteractionPrompt
                    : defaultPromptText;

                DrawInteractionPrompt(text);
            }
        }

        private void DrawCrosshairDot()
        {
            float centerX = Screen.width * 0.5f;
            float centerY = Screen.height * 0.5f;
            float dotSize = 4f;

            Color oldColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.55f);
            GUI.DrawTexture(new Rect(centerX - dotSize * 0.5f, centerY - dotSize * 0.5f, dotSize, dotSize), Texture2D.whiteTexture);
            GUI.color = oldColor;
        }

        private void DrawInteractionPrompt(string text)
        {
            GUIContent content = new GUIContent(text);
            GUIStyle style = new GUIStyle(GUI.skin.box)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            Vector2 size = style.CalcSize(content);
            size.x += 24f;
            size.y += 10f;

            // Position centered horizontally, slightly below center crosshair
            float x = (Screen.width - size.x) * 0.5f;
            float y = (Screen.height * 0.5f) + 35f;

            Color oldColor = GUI.color;
            GUI.color = new Color(0.12f, 0.12f, 0.15f, 0.88f);
            GUI.Box(new Rect(x, y, size.x, size.y), GUIContent.none);

            GUI.color = Color.white;
            GUI.Label(new Rect(x, y, size.x, size.y), content, style);
            GUI.color = oldColor;
        }

        private void OnDrawGizmosSelected()
        {
            if (playerCamera != null)
            {
                Gizmos.color = HasInteractableTarget ? Color.green : Color.cyan;
                Gizmos.DrawRay(playerCamera.transform.position, playerCamera.transform.forward * interactionRange);
            }
        }
    }
}
