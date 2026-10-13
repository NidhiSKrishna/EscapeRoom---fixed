using UnityEngine;
using UnityEngine.InputSystem;
using EscapeRoom.Interaction;
using EscapeRoom.Player;
using EscapeRoom.UI;
using EscapeRoom.Core;

namespace EscapeRoom.Stage2
{
    /// <summary>
    /// Narrative and cipher adapter for Stage 2 (Storage Room).
    /// Provides an interactive on-screen document reading modal for the Facility Shift Log,
    /// displaying the stage cipher ("The storage area may contain something useful..."),
    /// dynamic supervisor notes indicating the randomized search spot, and facility instructions.
    /// Completes the MEMO_READ objective step on read.
    /// </summary>
    [DisallowMultipleComponent]
    public class Stage2CipherAdapter : MonoBehaviour, IInteractable
    {
        [Header("Stage Cipher & Narrative Text")]
        [TextArea(2, 4)]
        [SerializeField] private string cipherMessage = "The storage area may contain something useful. Look for anything that was left behind.";

        [TextArea(2, 3)]
        [SerializeField] private string introHint = "HINT [INTRO]: Power up the central console terminal and observe the security pattern sequence.";

        [TextArea(2, 3)]
        [SerializeField] private string observationHint = "HINT [OBSERVE]: Solve the logic sequence to unlock the vault combination, then check the facility log for the keycard location.";

        [TextArea(2, 3)]
        [SerializeField] private string encouragementHint = "HINT [ADVICE]: Keep calm. Once the keycard is in hand, bypass the sector security reader before the automated lockdown engages.";

        [Header("Interaction Settings")]
        [SerializeField] private string promptText = "Press E to read Shift Log";
        [SerializeField] private bool canInteract = true;

        private static Stage2CipherAdapter instance;
        public static Stage2CipherAdapter Instance => instance;
        public static bool IsDocumentOpen => instance != null && instance.isOpen;

        private bool isOpen = false;
        private int openedFrame = -1;
        private int readCount = 0;
        private PlayerLook cachedPlayerLook;

        public bool IsOpen => isOpen;
        public string InteractionPrompt => promptText;
        public string PromptText => promptText;
        public bool CanInteract => canInteract;

        public string CipherMessage => cipherMessage;
        public string IntroHint => introHint;
        public string ObservationHint => observationHint;
        public string EncouragementHint => encouragementHint;

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

        private void Start()
        {
            cachedPlayerLook = FindAnyObjectByType<PlayerLook>();
        }

        private void Update()
        {
            if (!isOpen || Time.frameCount == openedFrame) return;

            // Ensure cursor remains unlocked and visible while document is open
            if (Cursor.lockState != CursorLockMode.None || !Cursor.visible)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)
                {
                    CloseDocument();
                }
            }
        }

        public void Interact()
        {
            if (isOpen)
            {
                CloseDocument();
                return;
            }

            OpenDocument();
        }

        public void OpenDocument()
        {
            isOpen = true;
            openedFrame = Time.frameCount;
            readCount++;

            Stage2Audio.Instance?.PlayButtonPress();

            // Notify objective system that the facility memo was read
            ObjectiveManager.Instance?.CompleteFlag("MEMO_READ");

            // Unlock cursor for GUI reading
            if (cachedPlayerLook == null)
            {
                cachedPlayerLook = FindAnyObjectByType<PlayerLook>();
            }
            if (cachedPlayerLook != null)
            {
                cachedPlayerLook.SetCursorLock(false);
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            Debug.Log($"<color=#5cb85c>[Stage2CipherAdapter]</color> Opened Facility Shift Log document (read #{readCount}).");
        }

        public void CloseDocument()
        {
            if (!isOpen) return;

            isOpen = false;
            Stage2Audio.Instance?.PlayButtonPress();

            // Re-lock cursor to first-person view
            if (cachedPlayerLook != null)
            {
                cachedPlayerLook.SetCursorLock(true);
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            Debug.Log("[Stage2CipherAdapter] Closed Facility Shift Log document.");
        }

        private void OnGUI()
        {
            if (!isOpen) return;

            float winW = Mathf.Min(700f, Screen.width * 0.90f);
            float winH = Mathf.Min(560f, Screen.height * 0.90f);
            float winX = (Screen.width - winW) * 0.5f;
            float winY = (Screen.height - winH) * 0.5f;

            Color oldColor = GUI.color;

            // Background Board / Slate Chassis
            GUI.color = new Color(0.08f, 0.10f, 0.14f, 0.98f);
            GUI.Box(new Rect(winX, winY, winW, winH), GUIContent.none);

            // Document Border Line (Cyan / High-contrast accent)
            GUI.color = new Color(0.20f, 0.75f, 0.85f, 0.90f);
            GUI.Box(new Rect(winX + 4, winY + 4, winW - 8, winH - 8), GUIContent.none);

            // Paper Body Chassis (Parchment clipboard surface)
            GUI.color = new Color(0.12f, 0.14f, 0.18f, 1f);
            GUI.Box(new Rect(winX + 8, winY + 8, winW - 16, winH - 16), GUIContent.none);

            // Header Banner
            GUI.color = new Color(0.16f, 0.22f, 0.30f, 1f);
            GUI.Box(new Rect(winX + 12, winY + 12, winW - 24, 60f), GUIContent.none);

            // Title Label (White #FFFFFF)
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUI.color = Color.white;
            GUI.Label(new Rect(winX + 16, winY + 16, winW - 32, 28f), "FACILITY STORAGE SECTOR // SHIFT LOG", titleStyle);

            var subTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.60f, 0.80f, 0.95f) }
            };
            GUI.Label(new Rect(winX + 16, winY + 44, winW - 32, 20f), "DOCUMENT CLASSIFICATION: RESTRICTED // INTERNAL FACILITY LOG", subTitleStyle);

            // Content Area
            float curY = winY + 84f;

            // Section 1: Facility Directive / Cipher
            var sectionHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.40f, 0.90f, 1.0f) }
            };
            GUI.Label(new Rect(winX + 24, curY, winW - 48, 20f), "► FACILITY DIRECTIVE & CIPHER MEMO:", sectionHeaderStyle);
            curY += 22f;

            var bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                wordWrap = true,
                normal = { textColor = Color.white }
            };

            GUI.Label(new Rect(winX + 32, curY, winW - 64, 40f), $"\"{cipherMessage}\"", bodyStyle);
            curY += 46f;

            // Section 2: Shift Supervisor Log & Dynamic Clue
            GUI.Label(new Rect(winX + 24, curY, winW - 48, 20f), "► SHIFT SUPERVISOR LOG NOTE (Shift 14-B):", sectionHeaderStyle);
            curY += 22f;

            string dynamicClue = "FACILITY LOG: 'Check the designated storage containers for the Terminal Security Chip.'";
            string targetSpot = "UNKNOWN";
            string targetLoc = "Check facility containers.";
            var gen = Stage2PuzzleGenerator.Instance;
            if (gen != null && gen.ActiveSearchSpot != null)
            {
                if (!string.IsNullOrEmpty(gen.ActiveSearchSpot.clueNote))
                    dynamicClue = gen.ActiveSearchSpot.clueNote;
                if (!string.IsNullOrEmpty(gen.ActiveSearchSpot.spotName))
                    targetSpot = gen.ActiveSearchSpot.spotName.ToUpper();
                if (!string.IsNullOrEmpty(gen.ActiveSearchSpot.locationDescription))
                    targetLoc = gen.ActiveSearchSpot.locationDescription;
            }

            // Highlighting Clue Box
            GUI.color = new Color(0.12f, 0.20f, 0.16f, 0.95f);
            GUI.Box(new Rect(winX + 28, curY, winW - 56, 76f), GUIContent.none);
            GUI.color = new Color(0.35f, 0.95f, 0.40f, 0.80f);
            GUI.Box(new Rect(winX + 29, curY + 1, winW - 58, 74f), GUIContent.none);

            var clueStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Normal,
                wordWrap = true,
                normal = { textColor = Color.white }
            };
            var targetHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1.0f, 0.85f, 0.25f) }
            };
            var locStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Italic,
                normal = { textColor = new Color(0.75f, 0.90f, 0.85f) }
            };

            GUI.color = Color.white;
            GUI.Label(new Rect(winX + 38, curY + 4, winW - 76, 36f), dynamicClue, clueStyle);
            GUI.Label(new Rect(winX + 38, curY + 40, winW - 76, 18f), $"► TARGET CONTAINER: [ {targetSpot} ]", targetHeaderStyle);
            GUI.Label(new Rect(winX + 38, curY + 56, winW - 76, 18f), $"  Location: {targetLoc}", locStyle);
            curY += 86f;

            // Section 3: Sector Evacuation Checklist
            GUI.Label(new Rect(winX + 24, curY, winW - 48, 20f), "► STANDARD RECOVERY PROCEDURE:", sectionHeaderStyle);
            curY += 22f;

            string procedures = 
                "1. Search the location specified above to retrieve the [Terminal Security Chip].\n" +
                "2. Insert the chip into the Pattern Console to unlock the Storage Vault code.\n" +
                "3. Enter the 3-digit combination on the Vault Keypad to retrieve the Master Keycard.\n" +
                "4. Authorize the Sector Security Reader next to the door to release the blast lock.";

            GUI.Label(new Rect(winX + 32, curY, winW - 64, 80f), procedures, bodyStyle);
            curY += 86f;

            // Progressive Hint Box if player examines multiple times
            if (readCount > 1)
            {
                string hintText = introHint;
                if (readCount == 3) hintText = observationHint;
                else if (readCount >= 4) hintText = encouragementHint;

                GUI.color = new Color(0.30f, 0.24f, 0.12f, 0.90f);
                GUI.Box(new Rect(winX + 28, curY, winW - 56, 44f), GUIContent.none);
                GUI.color = new Color(0.95f, 0.75f, 0.25f, 0.80f);
                GUI.Box(new Rect(winX + 29, curY + 1, winW - 58, 42f), GUIContent.none);

                var hintStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Italic,
                    wordWrap = true,
                    normal = { textColor = Color.white }
                };
                GUI.color = Color.white;
                GUI.Label(new Rect(winX + 38, curY + 4, winW - 76, 36f), hintText, hintStyle);
                curY += 50f;
            }

            // Close Button
            float btnW = 260f;
            float btnH = 42f;
            float btnX = winX + (winW - btnW) * 0.5f;
            float btnY = winY + winH - btnH - 18f;

            var closeBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            if (GUI.Button(new Rect(btnX, btnY, btnW, btnH), "CLOSE LOG (Press E / Space)", closeBtnStyle))
            {
                CloseDocument();
            }

            GUI.color = oldColor;
        }

        public string GetContextualHint(bool patternSolved, bool containerOpened, bool keycardHeld)
        {
            if (!patternSolved) return introHint;
            if (!containerOpened) return observationHint;
            if (!keycardHeld) return "Examine the designated search location to retrieve the security keycard.";
            return encouragementHint;
        }
    }
}
