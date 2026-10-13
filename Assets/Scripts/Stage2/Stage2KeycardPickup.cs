using UnityEngine;
using EscapeRoom.Core;
using EscapeRoom.Interaction;
using EscapeRoom.UI;

namespace EscapeRoom.Stage2
{
    /// <summary>
    /// Interactive keycard pickup resting inside the locked storage container.
    /// Picking up the keycard adds it to player inventory, completes the KEYCARD_ACQUIRED objective flag,
    /// and immediately triggers the 10-second adrenaline security alarm event.
    /// </summary>
    [DisallowMultipleComponent]
    public class Stage2KeycardPickup : MonoBehaviour, IInteractable
    {
        [Header("Configuration")]
        [SerializeField] private string itemId = "keycard";
        [SerializeField] private string displayName = "Storage Keycard";
        [SerializeField] private string promptText = "Press E to pick up Security Keycard";
        [SerializeField] private bool canInteract = true;

        [Header("Visual Effects")]
        [Tooltip("Slight idle bobbing and rotation.")]
        [SerializeField] private bool idleFloat = true;

        private Vector3 startPos;

        public string InteractionPrompt => promptText;
        public string PromptText => promptText;
        public bool CanInteract => canInteract;

        private void Start()
        {
            startPos = transform.position;
        }

        private void OnEnable()
        {
            startPos = transform.position;
        }

        private void Update()
        {
            if (idleFloat)
            {
                transform.Rotate(Vector3.up, 45f * Time.deltaTime, Space.World);
                float yOffset = Mathf.Sin(Time.time * 3f) * 0.04f;
                transform.position = startPos + new Vector3(0f, yOffset, 0f);
            }
        }

        public void Interact()
        {
            if (!canInteract) return;
            canInteract = false;

            Stage2Audio.Instance?.PlayItemPickup();

            // Add to Player Inventory
            var inv = FindAnyObjectByType<InventorySystem>();
            if (inv != null)
            {
                var item = ItemData.CreateRuntimeInstance(itemId, displayName, "High-clearance security card retrieved from storage room vault.");
                inv.AddItem(item);
            }

            // Register in Stage 2 Puzzle Generator
            var gen = Stage2PuzzleGenerator.Instance;
            if (gen != null)
            {
                gen.RegisterKeycardFound();
            }

            // Fire Objective Flag
            ObjectiveManager.Instance?.CompleteFlag("KEYCARD_ACQUIRED");

            // Feedback Message
            FeedbackHUD.ShowMessage("ACQUIRED: [Storage Keycard]! Alarms triggered!", new Color(0.98f, 0.90f, 0.35f));

            // Trigger the 10-second Adrenaline Security Event
            var alarm = Stage2SecurityAlarmEvent.Instance;
            if (alarm != null)
            {
                alarm.TriggerAlarm();
            }

            // Hide/destroy pickup
            gameObject.SetActive(false);
            Debug.Log("[Stage2KeycardPickup] Keycard collected by player. Security alarm engaged.");
        }
    }
}
