using UnityEngine;

namespace EscapeRoom.Interaction
{
    /// <summary>
    /// Specialized PickupItem for physical keys required by LockedContainers or Doors.
    /// Default key ID is 'lockbox_key'.
    /// </summary>
    [DisallowMultipleComponent]
    public class KeyItem : PickupItem
    {
        [Header("Key Defaults")]
        [SerializeField] private string defaultKeyId = "lockbox_key";
        [SerializeField] private string defaultKeyName = "Lockbox Key";

        private void Reset()
        {
            ConfigureItem(defaultKeyId, defaultKeyName, "A solid brass key that fits the old lockbox on the table.");
        }

        private void Awake()
        {
            // Auto-migrate legacy 'room_key' or unassigned ID to 'lockbox_key'
            if (string.IsNullOrEmpty(ItemId) || ItemId == "item_id" || ItemId == "room_key")
            {
                ConfigureItem(defaultKeyId, defaultKeyName, "A solid brass key that fits the old lockbox on the table.");
            }
        }
    }
}
