using System;
using UnityEngine;

namespace EscapeRoom.Core
{
    /// <summary>
    /// Data definition for inventory items, keys, and puzzle collectibles.
    /// Can be used as a ScriptableObject asset or instantiated in code/inspector.
    /// </summary>
    [CreateAssetMenu(fileName = "NewItemData", menuName = "Escape Room/Item Data")]
    public class ItemData : ScriptableObject
    {
        [Header("Item Identification")]
        [Tooltip("Unique string identifier for this item (e.g. 'lockbox_key').")]
        [SerializeField] private string itemId = "item_id";

        [Tooltip("User-facing display name of the item.")]
        [SerializeField] private string displayName = "New Item";

        [Header("Item Details")]
        [TextArea(2, 4)]
        [Tooltip("Optional descriptive text for inspection or journal entries.")]
        [SerializeField] private string description = "";

        public string ItemId => itemId;
        public string DisplayName => displayName;
        public string Description => description;

        public static ItemData CreateRuntimeInstance(string id, string name, string desc = "")
        {
            var data = CreateInstance<ItemData>();
            data.itemId = id;
            data.displayName = name;
            data.description = desc;
            return data;
        }
    }
}
