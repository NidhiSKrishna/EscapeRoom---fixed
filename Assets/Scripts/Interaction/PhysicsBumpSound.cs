using UnityEngine;
using EscapeRoom.Audio;

namespace EscapeRoom.Interaction
{
    /// <summary>
    /// Attach this component to physical interactable objects (Lockbox, Cabinet, Door etc.)
    /// to produce a subtle bump/thud sound when the player runs into them.
    ///
    /// Uses a cooldown to avoid sound spam from continuous contact.
    /// Minimum impact velocity threshold prevents triggering on very light touches.
    /// </summary>
    [DisallowMultipleComponent]
    public class PhysicsBumpSound : MonoBehaviour
    {
        [Header("Bump Detection")]
        [Tooltip("Minimum relative velocity (m/s) to trigger a bump sound.")]
        [SerializeField] private float minVelocityThreshold = 0.8f;

        [Tooltip("Minimum seconds between consecutive bump sounds to prevent spam.")]
        [SerializeField] private float cooldownSeconds = 0.6f;

        [Header("Volume")]
        [Range(0f, 1f)]
        [SerializeField] private float volumeScale = 0.75f;

        private float cooldownTimer = 0f;

        private void Update()
        {
            if (cooldownTimer > 0f)
                cooldownTimer -= Time.deltaTime;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (cooldownTimer > 0f) return;

            // Only trigger for significant collisions (ignore micro-jitter)
            float speed = collision.relativeVelocity.magnitude;
            if (speed < minVelocityThreshold) return;

            // Ignore collisions with other static geometry (only care about dynamic objects
            // such as the player character controller's push force)
            if (collision.rigidbody == null) return;

            cooldownTimer = cooldownSeconds;

            // Play at collision contact point for proper 3D positioning
            Vector3 contactPoint = collision.contacts.Length > 0
                ? collision.contacts[0].point
                : transform.position;

            EscapeRoomAudio.PlayAt(EscapeRoomAudio.SoundId.ObjectBump, contactPoint, volumeScale);
        }
    }
}
