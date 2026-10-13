using UnityEngine;
using EscapeRoom.Core;

namespace EscapeRoom.Interaction
{
    /// <summary>
    /// World item that can be picked up by the player and placed into the InventorySystem.
    /// Implements IInteractable.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class PickupItem : MonoBehaviour, IInteractable
    {
        [Header("Item Definition")]
        [Tooltip("ScriptableObject data definition for this item.")]
        [SerializeField] private ItemData itemData;

        [Header("Inline Fallback Definition")]
        [Tooltip("Used if ItemData asset is not assigned.")]
        [SerializeField] private string fallbackItemId = "item_id";
        [SerializeField] private string fallbackDisplayName = "Collectible Item";
        [TextArea(2, 3)]
        [SerializeField] private string fallbackDescription = "";

        [Header("Pickup Behavior")]
        [Tooltip("Whether to destroy the GameObject (true) or just deactivate it (false) on pickup.")]
        [SerializeField] private bool destroyOnPickup = false;

        [Tooltip("Format string for the interaction prompt ({0} is replaced by the item display name).")]
        [SerializeField] private string promptFormat = "Press E to pick up {0}";

        private bool isCollected = false;

        public ItemData ItemData
        {
            get => itemData;
            set => itemData = value;
        }

        public string ItemId => itemData != null ? itemData.ItemId : fallbackItemId;
        public string ItemDisplayName => itemData != null ? itemData.DisplayName : fallbackDisplayName;

        public string InteractionPrompt => $"[{EscapeRoom.Core.InputConfig.InteractKeyName}] Pick up {ItemDisplayName}";
        public bool CanInteract => !isCollected && gameObject.activeInHierarchy;

        public void ConfigureItem(string id, string displayName, string description = "")
        {
            fallbackItemId = id;
            fallbackDisplayName = displayName;
            fallbackDescription = description;
            if (itemData != null && itemData.ItemId != id)
            {
                itemData = null;
            }
        }

        public void Interact()
        {
            if (isCollected) return;

            InventorySystem inventory = InventorySystem.Instance;
            if (inventory == null)
            {
                inventory = FindAnyObjectByType<InventorySystem>();
            }

            if (inventory == null)
            {
                Debug.LogError($"[PickupItem] Cannot pick up '{ItemDisplayName}': No InventorySystem found in scene!");
                return;
            }

            bool added;
            if (itemData != null)
            {
                added = inventory.AddItem(itemData);
            }
            else
            {
                added = inventory.AddItem(fallbackItemId, fallbackDisplayName, fallbackDescription);
            }

            if (added)
            {
                isCollected = true;
                Debug.Log($"<color=#5cb85c><b>[PickupItem]</b></color> Successfully collected '{ItemDisplayName}'.");

                // Play pickup sound
                EscapeRoom.Audio.EscapeRoomAudio.PlayAt(
                    EscapeRoom.Audio.EscapeRoomAudio.SoundId.KeyPickup,
                    transform.position);

                if (destroyOnPickup)
                {
                    Destroy(gameObject);
                }
                else
                {
                    gameObject.SetActive(false);
                }
            }
        }
    }
}
