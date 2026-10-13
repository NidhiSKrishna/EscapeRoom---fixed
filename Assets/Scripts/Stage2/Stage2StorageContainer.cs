using System;
using UnityEngine;
using UnityEngine.InputSystem;
using EscapeRoom.Interaction;
using EscapeRoom.Player;
using EscapeRoom.UI;

namespace EscapeRoom.Stage2
{
    /// <summary>
    /// Interactive locked storage container for Stage 2.
    /// The player opens this container by entering the 3-digit combination learned from the pattern terminal.
    /// Upon entering the correct combination, it plays a mechanical unlock sound, smoothly opens its lid
    /// via a physical animation, and reveals the glowing Security Keycard inside.
    /// </summary>
    [DisallowMultipleComponent]
    public class Stage2StorageContainer : MonoBehaviour, IInteractable
    {
        private static Stage2StorageContainer instance;
        public static Stage2StorageContainer Instance => instance;

        [Header("Interaction Settings")]
        [SerializeField] private string promptText = "Press E to open Locked Storage Container";
        [SerializeField] private bool canInteract = true;

        [Header("Physical Animation")]
        [Tooltip("The lid or door transform to animate upon unlocking.")]
        [SerializeField] private Transform lidTransform;
        [SerializeField] private Vector3 openRotationEuler = new Vector3(-85f, 0f, 0f);
        [SerializeField] private float animationDuration = 1.2f;

        [Header("Contents")]
        [Tooltip("The keycard GameObject placed inside the container.")]
        [SerializeField] private GameObject contentsKeycard;

        [Header("State")]
        [SerializeField] private bool isUnlocked = false;
        private bool isOpen = false;
        private int openedFrame = -1;
        private string enteredCode = "";
        private string statusMessage = "ENTER 3-DIGIT COMBINATION";
        private Color statusColor = Color.white;
        private float statusTimer = 0f;

        private Quaternion closedRotation;
        private Quaternion targetOpenRotation;
        private bool isAnimating = false;
        private float animationProgress = 0f;

        public static bool IsKeypadOpen => instance != null && instance.isOpen;

        public string InteractionPrompt => PromptText;
        public string PromptText => isUnlocked ? "Storage Container (Unlocked)" : promptText;
        public bool CanInteract => canInteract && !isUnlocked && !isOpen;
        public bool IsUnlocked => isUnlocked;
        public Transform LidTransform { get => lidTransform; set => lidTransform = value; }
        public GameObject ContentsKeycard { get => contentsKeycard; set => contentsKeycard = value; }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }
            instance = this;

            if (lidTransform != null)
            {
                closedRotation = lidTransform.localRotation;
                targetOpenRotation = Quaternion.Euler(openRotationEuler) * closedRotation;
            }

            if (contentsKeycard != null)
            {
                EnsureKeycardPosition();
                contentsKeycard.SetActive(isUnlocked);
            }
        }

        private void EnsureKeycardPosition()
        {
            if (contentsKeycard != null)
            {
                Vector3 lp = contentsKeycard.transform.localPosition;
                if (lp.y < 0.52f)
                {
                    lp.y = 0.56f;
                    contentsKeycard.transform.localPosition = lp;
                }
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        public void ConfigureComponents(Transform lid, GameObject keycard)
        {
            lidTransform = lid;
            contentsKeycard = keycard;

            if (lidTransform != null)
            {
                closedRotation = lidTransform.localRotation;
                targetOpenRotation = Quaternion.Euler(openRotationEuler) * closedRotation;
            }

            if (contentsKeycard != null)
            {
                EnsureKeycardPosition();
                contentsKeycard.SetActive(isUnlocked);
            }
        }

        public void Interact()
        {
            if (isUnlocked)
            {
                FeedbackHUD.ShowMessage("Container is already open.", Color.white);
                return;
            }

            OpenKeypad();
        }

        public void OpenKeypad()
        {
            isOpen = true;
            openedFrame = Time.frameCount;
            enteredCode = "";
            statusMessage = "ENTER 3-DIGIT COMBINATION";
            statusColor = Color.white;
            Stage2Audio.Instance?.PlayButtonPress();

            var look = FindAnyObjectByType<PlayerLook>();
            if (look != null)
            {
                look.SetCursorLock(false);
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void CloseKeypad()
        {
            if (!isOpen) return;
            isOpen = false;
            Stage2Audio.Instance?.PlayButtonPress();

            var look = FindAnyObjectByType<PlayerLook>();
            if (look != null)
            {
                look.SetCursorLock(true);
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void Update()
        {
            // Process smooth lid opening animation
            if (isAnimating && lidTransform != null)
            {
                animationProgress += Time.deltaTime / animationDuration;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(animationProgress));
                lidTransform.localRotation = Quaternion.Slerp(closedRotation, targetOpenRotation, t);

                if (animationProgress >= 1f)
                {
                    isAnimating = false;
                }
            }

            if (!isOpen || Time.frameCount == openedFrame) return;

            // Ensure cursor remains unlocked and visible while keypad is active
            if (Cursor.lockState != CursorLockMode.None || !Cursor.visible)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (statusTimer > 0f)
            {
                statusTimer -= Time.deltaTime;
                if (statusTimer <= 0f)
                {
                    statusMessage = "ENTER 3-DIGIT COMBINATION";
                    statusColor = Color.white;
                }
            }

            HandleKeyboardInput();
        }

        private void HandleKeyboardInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.escapeKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame)
            {
                CloseKeypad();
                return;
            }

            // Numeric keys
            for (int d = 0; d <= 9; d++)
            {
                Key digitKey = Key.Digit0 + d;
                Key numKey = Key.Numpad0 + d;
                if (keyboard[digitKey].wasPressedThisFrame || keyboard[numKey].wasPressedThisFrame)
                {
                    AppendDigit(d.ToString());
                    return;
                }
            }

            if (keyboard.backspaceKey.wasPressedThisFrame)
            {
                ClearCode();
            }

            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
            {
                SubmitCode();
            }
        }

        private void AppendDigit(string digit)
        {
            if (enteredCode.Length < 3)
            {
                enteredCode += digit;
                Stage2Audio.Instance?.PlayButtonPress();
            }
        }

        private void ClearCode()
        {
            enteredCode = "";
            Stage2Audio.Instance?.PlayButtonPress();
        }

        private void SubmitCode()
        {
            if (enteredCode.Length != 3)
            {
                statusMessage = "ENTER FULL 3-DIGIT CODE";
                statusColor = new Color(0.95f, 0.40f, 0.40f);
                statusTimer = 2.5f;
                Stage2Audio.Instance?.PlayWrongAnswer();
                return;
            }

            var gen = Stage2PuzzleGenerator.Instance;
            if (gen != null && gen.ValidateContainerCode(enteredCode))
            {
                UnlockContainer();
            }
            else
            {
                statusMessage = "COMBINATION REJECTED";
                statusColor = new Color(0.95f, 0.35f, 0.35f);
                statusTimer = 3f;
                Stage2Audio.Instance?.PlayWrongAnswer();
                enteredCode = "";
            }
        }

        public void UnlockContainer()
        {
            isUnlocked = true;
            isOpen = false;

            var look = FindAnyObjectByType<PlayerLook>();
            if (look != null)
            {
                look.SetCursorLock(true);
            }

            Stage2Audio.Instance?.PlayContainerUnlock();

            // Trigger physical lid animation
            isAnimating = true;
            animationProgress = 0f;

            // Reveal keycard above vault surface
            if (contentsKeycard != null)
            {
                EnsureKeycardPosition();
                contentsKeycard.SetActive(true);

                var kcCol = contentsKeycard.GetComponent<BoxCollider>();
                if (kcCol == null) kcCol = contentsKeycard.AddComponent<BoxCollider>();
                kcCol.size = new Vector3(0.60f, 0.50f, 0.60f);
            }

            // Disable container root collider so it doesn't block raycasts to keycard
            var rootCol = GetComponent<Collider>();
            if (rootCol != null)
            {
                rootCol.enabled = false;
            }

            // Disable child body colliders that could intercept raycasts
            var allColliders = GetComponentsInChildren<Collider>(true);
            foreach (var col in allColliders)
            {
                if (contentsKeycard != null && (col.gameObject == contentsKeycard || col.transform.IsChildOf(contentsKeycard.transform)))
                {
                    continue; // Keep keycard collider active
                }
                if (col.gameObject != gameObject && (col.name.Contains("VaultBody") || col.name.Contains("VaultKeypad") || col.name.Contains("VaultLid")))
                {
                    col.enabled = false;
                }
            }

            FeedbackHUD.ShowMessage("CONTAINER UNLOCKED! Security Keycard revealed.", new Color(0.35f, 0.95f, 0.40f));
            Debug.Log("[Stage2StorageContainer] Container successfully unlocked.");
        }

        private void OnGUI()
        {
            if (!isOpen) return;

            float winW = 360f;
            float winH = 460f;
            float winX = (Screen.width - winW) * 0.5f;
            float winY = (Screen.height - winH) * 0.5f;

            Color oldColor = GUI.color;

            // Chassis
            GUI.color = new Color(0.08f, 0.10f, 0.14f, 0.96f);
            GUI.Box(new Rect(winX, winY, winW, winH), GUIContent.none);

            // Border
            GUI.color = new Color(0.35f, 0.45f, 0.55f, 0.90f);
            GUI.Box(new Rect(winX + 4, winY + 4, winW - 8, winH - 8), GUIContent.none);

            // Title (White #FFFFFF)
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUI.color = Color.white;
            GUI.Label(new Rect(winX + 10, winY + 14, winW - 20, 24f), "HEAVY STORAGE SAFE // VAULT", titleStyle);

            // Status Label
            var statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = statusColor }
            };
            GUI.Label(new Rect(winX + 10, winY + 40, winW - 20, 20f), statusMessage, statusStyle);

            // Digits Display Box
            float dispW = 260f;
            float dispH = 55f;
            float dispX = winX + (winW - dispW) * 0.5f;
            float dispY = winY + 68f;

            GUI.color = new Color(0.03f, 0.05f, 0.08f, 1f);
            GUI.Box(new Rect(dispX, dispY, dispW, dispH), GUIContent.none);

            string displayStr = "";
            for (int i = 0; i < 3; i++)
            {
                if (i < enteredCode.Length)
                    displayStr += $" {enteredCode[i]} ";
                else
                    displayStr += " _ ";
            }

            var dispStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.98f, 0.95f, 0.40f) }
            };
            GUI.color = Color.white;
            GUI.Label(new Rect(dispX, dispY, dispW, dispH), displayStr, dispStyle);

            // Keypad Grid (1-9, Clear, 0, Enter)
            float startKx = winX + 35f;
            float startKy = winY + 140f;
            float btnW = 85f;
            float btnH = 50f;
            float gapX = 14f;
            float gapY = 12f;

            var keyBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            for (int r = 0; r < 3; r++)
            {
                for (int c = 0; c < 3; c++)
                {
                    int val = r * 3 + c + 1;
                    float bx = startKx + c * (btnW + gapX);
                    float by = startKy + r * (btnH + gapY);

                    if (GUI.Button(new Rect(bx, by, btnW, btnH), val.ToString(), keyBtnStyle))
                    {
                        AppendDigit(val.ToString());
                    }
                }
            }

            // Bottom row: CLEAR, 0, ENTER
            float lastRowY = startKy + 3 * (btnH + gapY);

            var actionBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            if (GUI.Button(new Rect(startKx, lastRowY, btnW, btnH), "CLR", actionBtnStyle))
            {
                ClearCode();
            }

            if (GUI.Button(new Rect(startKx + (btnW + gapX), lastRowY, btnW, btnH), "0", keyBtnStyle))
            {
                AppendDigit("0");
            }

            if (GUI.Button(new Rect(startKx + 2 * (btnW + gapX), lastRowY, btnW, btnH), "ENT", actionBtnStyle))
            {
                SubmitCode();
            }

            // Close button
            float closeW = 160f;
            float closeH = 32f;
            float closeX = winX + (winW - closeW) * 0.5f;
            float closeY = winY + winH - 44f;

            if (GUI.Button(new Rect(closeX, closeY, closeW, closeH), "CANCEL [ESC / E]", actionBtnStyle))
            {
                CloseKeypad();
            }

            GUI.color = oldColor;
        }
    }
}
