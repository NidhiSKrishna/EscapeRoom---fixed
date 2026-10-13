using UnityEngine;
using EscapeRoom.Core;
using EscapeRoom.Puzzle;

namespace EscapeRoom.Interaction
{
    /// <summary>
    /// Stage 2 progression adapter: connects Storage Room puzzle events to ObjectiveManager flags.
    /// Five-step Stage 2 progression:
    ///   Step 1 CLUE_FOUND           → find the symbol clue note
    ///   Step 2 COMBO_DISCOVERED     → complete the symbol clue inspection (auto via clue open)
    ///   Step 3 CABINET_UNLOCKED     → enter correct symbol combination
    ///   Step 4 ACCESS_CARD_ACQUIRED → pick up access card
    ///   Step 5 CORRIDOR_UNLOCKED    → use card on security reader
    ///
    /// This adapter is SEPARATE from ObjectiveProgressionAdapter (Stage 1) to avoid interference.
    /// </summary>
    [DisallowMultipleComponent]
    public class StorageRoomProgressionAdapter : MonoBehaviour
    {
        private static StorageRoomProgressionAdapter instance;
        public static StorageRoomProgressionAdapter Instance => instance;

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
            if (instance == this) instance = null;
        }

        private void OnEnable()
        {
            SymbolClueInteractable.OnAnySymbolClueOpened += HandleClueOpened;
            SymbolCombinationCabinet.OnAnyCabinetUnlocked += HandleCabinetUnlocked;
            SymbolCombinationCabinet.OnAnyCabinetOpened += HandleCabinetOpened;
            SecurityCardReader.OnAnyAccessGranted += HandleAccessGranted;

            if (InventorySystem.Instance != null)
                InventorySystem.Instance.OnItemAdded += HandleItemAdded;
        }

        private void OnDisable()
        {
            SymbolClueInteractable.OnAnySymbolClueOpened -= HandleClueOpened;
            SymbolCombinationCabinet.OnAnyCabinetUnlocked -= HandleCabinetUnlocked;
            SymbolCombinationCabinet.OnAnyCabinetOpened -= HandleCabinetOpened;
            SecurityCardReader.OnAnyAccessGranted -= HandleAccessGranted;

            if (InventorySystem.Instance != null)
                InventorySystem.Instance.OnItemAdded -= HandleItemAdded;
        }

        private void Start()
        {
            // Re-bind InventorySystem in case it was not ready in OnEnable
            if (InventorySystem.Instance != null)
            {
                InventorySystem.Instance.OnItemAdded -= HandleItemAdded;
                InventorySystem.Instance.OnItemAdded += HandleItemAdded;
            }
        }

        // ── Event handlers ───────────────────────────────────────────────────
        private void HandleClueOpened()
        {
            // Clue found → completes Step 1 AND Step 2 together
            // (seeing the clue = finding AND discovering the combination)
            ObjectiveManager.Instance?.CompleteFlag("CLUE_FOUND");
            ObjectiveManager.Instance?.CompleteFlag("COMBO_DISCOVERED");
        }

        private void HandleCabinetUnlocked()
        {
            ObjectiveManager.Instance?.CompleteFlag("CABINET_UNLOCKED");
        }

        private void HandleCabinetOpened()
        {
            // Opening the cabinet doesn't advance its own step — the access card pickup does.
        }

        private void HandleItemAdded(ItemData item)
        {
            if (item == null) return;
            if (string.Equals(item.ItemId, "access_card", System.StringComparison.OrdinalIgnoreCase))
            {
                ObjectiveManager.Instance?.CompleteFlag("ACCESS_CARD_ACQUIRED");
            }
        }

        private void HandleAccessGranted()
        {
            ObjectiveManager.Instance?.CompleteFlag("CORRIDOR_UNLOCKED");
        }

        // ── Public notification hooks (callable from UnityEvents) ─────────────
        public void NotifyClueFound()      => ObjectiveManager.Instance?.CompleteFlag("CLUE_FOUND");
        public void NotifyComboDiscovered() => ObjectiveManager.Instance?.CompleteFlag("COMBO_DISCOVERED");
        public void NotifyCabinetUnlocked() => ObjectiveManager.Instance?.CompleteFlag("CABINET_UNLOCKED");
        public void NotifyCardAcquired()   => ObjectiveManager.Instance?.CompleteFlag("ACCESS_CARD_ACQUIRED");
        public void NotifyCorridorUnlocked() => ObjectiveManager.Instance?.CompleteFlag("CORRIDOR_UNLOCKED");
    }
}
