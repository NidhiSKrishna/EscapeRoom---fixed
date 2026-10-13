using System;
using UnityEngine;
using EscapeRoom.Core;
using EscapeRoom.Puzzle;

namespace EscapeRoom.Interaction
{
    /// <summary>
    /// Decoupled progression adapter connecting gameplay events to ObjectiveManager flags.
    /// Preserves existing gameplay logic by listening to standard events and callbacks.
    /// </summary>
    [DisallowMultipleComponent]
    public class ObjectiveProgressionAdapter : MonoBehaviour
    {
        private static ObjectiveProgressionAdapter instance;
        public static ObjectiveProgressionAdapter Instance => instance;

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

        private void OnEnable()
        {
            // Subscribe to static puzzle and interaction events
            LockedContainer.OnAnyContainerOpened += HandleContainerOpened;
            ClueInteractable.OnAnyClueOpened += HandleClueOpened;
            KeypadController.OnAnyCodeAccepted += HandleCodeAccepted;

            // Subscribe to exit code display — fires EXIT_NOTE_READ when on-screen code panel opens
            EscapeRoom.UI.ExitCodeDisplayUI.OnCodeDisplayed += HandleCodeDisplayed;

            // Subscribe to inventory changes
            if (InventorySystem.Instance != null)
            {
                InventorySystem.Instance.OnItemAdded += HandleItemAdded;
            }
        }

        private void OnDisable()
        {
            LockedContainer.OnAnyContainerOpened -= HandleContainerOpened;
            ClueInteractable.OnAnyClueOpened -= HandleClueOpened;
            KeypadController.OnAnyCodeAccepted -= HandleCodeAccepted;
            EscapeRoom.UI.ExitCodeDisplayUI.OnCodeDisplayed -= HandleCodeDisplayed;

            if (InventorySystem.Instance != null)
            {
                InventorySystem.Instance.OnItemAdded -= HandleItemAdded;
            }
        }

        private void Start()
        {
            // Re-bind to InventorySystem once scene is initialized
            if (InventorySystem.Instance != null)
            {
                InventorySystem.Instance.OnItemAdded -= HandleItemAdded;
                InventorySystem.Instance.OnItemAdded += HandleItemAdded;
            }
        }

        private void HandleItemAdded(ItemData item)
        {
            if (item == null) return;

            string id = item.ItemId;
            if (string.Equals(id, "lockbox_key", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(id, "room_key", StringComparison.OrdinalIgnoreCase))
            {
                NotifyLockboxKeyAcquired();
            }
            else if (string.Equals(id, "keycard", StringComparison.OrdinalIgnoreCase))
            {
                NotifyKeycardAcquired();
            }
            else if (string.Equals(id, "final_key", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(id, "cellar_key", StringComparison.OrdinalIgnoreCase))
            {
                NotifyFinalExitUnlocked();
            }
        }

        private void HandleContainerOpened()
        {
            NotifyLockboxOpened();
        }

        private void HandleClueOpened()
        {
            NotifyExitNoteRead();
        }

        private void HandleCodeDisplayed()
        {
            // The on-screen EXIT KEYPAD CODE panel has been shown — treat as EXIT_NOTE_READ.
            // Safe to call even if ClueInteractable also fires later: ObjectiveManager.CompleteFlag
            // silently ignores already-completed flags.
            NotifyExitNoteRead();
        }

        private void HandleCodeAccepted()
        {
            NotifyExitCodeAccepted();
        }

        // Public hook methods for progression triggers
        public void NotifyLockboxKeyAcquired()
        {
            ObjectiveManager.Instance?.CompleteFlag("LOCKBOX_KEY_ACQUIRED");
        }

        public void NotifyLockboxOpened()
        {
            ObjectiveManager.Instance?.CompleteFlag("LOCKBOX_OPENED");
        }

        public void NotifyExitNoteRead()
        {
            ObjectiveManager.Instance?.CompleteFlag("EXIT_NOTE_READ");
        }

        public void NotifyExitCodeAccepted()
        {
            ObjectiveManager.Instance?.CompleteFlag("EXIT_CODE_ACCEPTED");
        }

        public void NotifyKeycardAcquired()
        {
            ObjectiveManager.Instance?.CompleteFlag("KEYCARD_ACQUIRED");
        }

        public void NotifyCellarUnlocked()
        {
            ObjectiveManager.Instance?.CompleteFlag("CELLAR_UNLOCKED");
        }

        public void NotifyFinalExitUnlocked()
        {
            ObjectiveManager.Instance?.CompleteFlag("FINAL_EXIT_UNLOCKED");
        }
    }
}
