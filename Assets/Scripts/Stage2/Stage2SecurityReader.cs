using UnityEngine;
using EscapeRoom.Core;
using EscapeRoom.Interaction;
using EscapeRoom.Puzzle;
using EscapeRoom.UI;

namespace EscapeRoom.Stage2
{
    /// <summary>
    /// Wall-mounted security card reader controlling access to the exit door.
    /// Interacting with the reader while holding the storage keycard disarms the security alarm,
    /// marks the CELLAR_UNLOCKED objective flag, changes the status LED to green, and swings open the exit door.
    /// </summary>
    [DisallowMultipleComponent]
    public class Stage2SecurityReader : MonoBehaviour, IInteractable
    {
        [Header("Configuration")]
        [SerializeField] private string requiredItemId = "keycard";
        [SerializeField] private string promptText = "Press E to swipe Keycard on Security Reader";
        [SerializeField] private bool canInteract = true;

        [Header("Linked Door")]
        [SerializeField] private DoorController exitDoor;

        [Header("Indicator Light")]
        [SerializeField] private Light statusLed;
        [SerializeField] private Renderer readerRenderer;

        [Header("State")]
        [SerializeField] private bool isUnlocked = false;

        public string InteractionPrompt => PromptText;
        public string PromptText => isUnlocked ? "Security Reader (Access Granted)" : promptText;
        public bool CanInteract => canInteract && !isUnlocked;
        public bool IsUnlocked => isUnlocked;
        public DoorController ExitDoor { get => exitDoor; set => exitDoor = value; }
        public Light StatusLed { get => statusLed; set => statusLed = value; }

        public void Configure(DoorController door, Light led)
        {
            exitDoor = door;
            statusLed = led;
            if (statusLed != null)
            {
                statusLed.color = new Color(0.95f, 0.25f, 0.20f); // Red standby
            }
        }

        public void Interact()
        {
            if (isUnlocked)
            {
                FeedbackHUD.ShowMessage("Reader already verified. Exit door unlocked.", Color.white);
                return;
            }

            // Check if player has the required keycard
            var inv = FindAnyObjectByType<InventorySystem>();
            bool hasCard = (inv != null && inv.HasItem(requiredItemId)) || 
                           (Stage2PuzzleGenerator.Instance != null && Stage2PuzzleGenerator.Instance.IsKeycardFound);

            if (!hasCard)
            {
                Stage2Audio.Instance?.PlayWrongAnswer();
                FeedbackHUD.ShowMessage("ACCESS DENIED: Storage Keycard required.", new Color(0.95f, 0.35f, 0.35f));
                return;
            }

            // Access Granted
            isUnlocked = true;
            Stage2Audio.Instance?.PlayCorrectAnswer();

            // Disarm security alarm event
            var alarm = Stage2SecurityAlarmEvent.Instance;
            if (alarm != null)
            {
                alarm.DisarmAlarm();
            }

            // Update status LED
            if (statusLed != null)
            {
                statusLed.color = new Color(0.25f, 0.95f, 0.35f); // Green
            }

            // Complete objective step 6
            ObjectiveManager.Instance?.CompleteFlag("CELLAR_UNLOCKED");

            // Open linked exit door
            if (exitDoor != null)
            {
                exitDoor.Unlock();
            }

            FeedbackHUD.ShowMessage("ACCESS GRANTED: Cellar passage unlocked!", new Color(0.35f, 0.95f, 0.40f));
            Debug.Log("[Stage2SecurityReader] Security reader unlocked. Exit door opened.");
        }
    }
}
