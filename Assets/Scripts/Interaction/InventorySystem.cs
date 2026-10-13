using System;
using System.Collections.Generic;
using UnityEngine;
using EscapeRoom.Core;
using EscapeRoom.UI;

namespace EscapeRoom.Interaction
{
    /// <summary>
    /// Manages the player's inventory, collected items, and key verification.
    /// Typically mounted on the Player GameObject or accessible globally.
    /// </summary>
    [DisallowMultipleComponent]
    public class InventorySystem : MonoBehaviour
    {
        private static InventorySystem instance;
        public static InventorySystem Instance => instance;

        [Header("Inventory Settings")]
        [Tooltip("Whether the same item ID can be collected more than once.")]
        [SerializeField] private bool allowDuplicates = false;

        [Header("Debug / Inspector View")]
        [SerializeField] private List<string> collectedItemIds = new List<string>();

        private readonly Dictionary<string, ItemData> itemDatabase = new Dictionary<string, ItemData>(StringComparer.OrdinalIgnoreCase);

        public event Action<ItemData> OnItemAdded;
        public event Action<string> OnItemRemoved;

        public bool AllowDuplicates
        {
            get => allowDuplicates;
            set => allowDuplicates = value;
        }

        public IReadOnlyList<string> CollectedItemIds => collectedItemIds;

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

        /// <summary>
        /// Checks whether the player currently holds an item with the given ID.
        /// </summary>
        public bool HasItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return false;
            return collectedItemIds.Exists(id => string.Equals(id, itemId, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Adds an item to the inventory.
        /// Returns true if the item was added, false if duplicate was rejected.
        /// </summary>
        public bool AddItem(ItemData item)
        {
            if (item == null || string.IsNullOrEmpty(item.ItemId))
            {
                Debug.LogWarning("[InventorySystem] Attempted to add a null or invalid item.");
                return false;
            }

            if (!allowDuplicates && HasItem(item.ItemId))
            {
                Debug.Log($"[InventorySystem] Item '{item.ItemId}' already exists in inventory.");
                return false;
            }

            collectedItemIds.Add(item.ItemId);
            itemDatabase[item.ItemId] = item;

            Debug.Log($"<color=#5cb85c><b>[InventorySystem]</b></color> Added item: '{item.DisplayName}' ({item.ItemId}). Total items: {collectedItemIds.Count}.");
            FeedbackHUD.ShowMessage($"Acquired: {item.DisplayName}", new Color(0.98f, 0.85f, 0.35f));

            OnItemAdded?.Invoke(item);
            return true;
        }

        /// <summary>
        /// Adds an item by ID and display name without requiring a pre-existing ItemData asset.
        /// </summary>
        public bool AddItem(string itemId, string displayName = "", string description = "")
        {
            if (string.IsNullOrEmpty(itemId)) return false;

            if (string.IsNullOrEmpty(displayName))
            {
                displayName = itemId;
            }

            var runtimeData = ItemData.CreateRuntimeInstance(itemId, displayName, description);
            return AddItem(runtimeData);
        }

        /// <summary>
        /// Removes the first instance of the specified item ID from the inventory.
        /// Returns true if an item was removed.
        /// </summary>
        public bool RemoveItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return false;

            int index = collectedItemIds.FindIndex(id => string.Equals(id, itemId, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                collectedItemIds.RemoveAt(index);
                if (!HasItem(itemId))
                {
                    itemDatabase.Remove(itemId);
                }

                Debug.Log($"[InventorySystem] Removed item: '{itemId}'. Remaining: {collectedItemIds.Count}.");
                OnItemRemoved?.Invoke(itemId);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Retrieves the ItemData definition for a collected item, if available.
        /// </summary>
        public ItemData GetItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            itemDatabase.TryGetValue(itemId, out var data);
            return data;
        }
    }
}
