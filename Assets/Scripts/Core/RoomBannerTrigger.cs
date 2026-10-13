using UnityEngine;
using EscapeRoom.Player;
using EscapeRoom.UI;

namespace EscapeRoom.Core
{
    /// <summary>
    /// Volume trigger that detects the player entering a room/zone and displays the associated Room Banner.
    /// Configurable title and tip per trigger instance.
    /// Supports Attic, Storage Room, and Cellar transitions.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class RoomBannerTrigger : MonoBehaviour
    {
        [Header("Room Banner Configuration")]
        [Tooltip("Header title displayed on the room banner (e.g. ATTIC, STORAGE ROOM, THE CELLAR).")]
        [SerializeField] private string roomTitle = "ATTIC";

        [TextArea(2, 4)]
        [Tooltip("Tip / atmosphere subtitle text displayed beneath the room title.")]
        [SerializeField] private string tipText = "Search carefully. Something useful is hidden here.";

        [Tooltip("Whether this trigger should only activate once per session.")]
        [SerializeField] private bool triggerOnce = true;

        [Header("State")]
        [SerializeField] private bool hasTriggered = false;

        public string RoomTitle
        {
            get => roomTitle;
            set => roomTitle = value;
        }

        public string TipText
        {
            get => tipText;
            set => tipText = value;
        }

        public bool HasTriggered => hasTriggered;

        private void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }
        }

        private void Awake()
        {
            var col = GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (triggerOnce && hasTriggered) return;

            bool isPlayer = other.CompareTag("Player") ||
                            other.GetComponent<PlayerController>() != null ||
                            other.GetComponentInParent<PlayerController>() != null ||
                            other.GetComponent<CharacterController>() != null;

            if (!isPlayer) return;

            hasTriggered = true;
            Debug.Log($"<color=#337ab7><b>[RoomBannerTrigger]</b></color> Player entered zone '{roomTitle}'. Triggering banner.");

            if (BannerUI.Instance != null)
            {
                BannerUI.Instance.ShowBanner(roomTitle, tipText);
            }
            else
            {
                var banner = FindAnyObjectByType<BannerUI>();
                if (banner != null)
                {
                    banner.ShowBanner(roomTitle, tipText);
                }
            }
        }

        public void ResetTrigger()
        {
            hasTriggered = false;
        }
    }
}
