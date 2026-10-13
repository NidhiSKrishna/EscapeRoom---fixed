using UnityEngine;
using EscapeRoom.Player;

namespace EscapeRoom.Core
{
    /// <summary>
    /// Invisible volume trigger placed just beyond the exit doorway threshold.
    /// Calls GameManager.CompleteEscape() when the Player steps through.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class EscapeTrigger : MonoBehaviour
    {
        [Header("State")]
        [SerializeField] private bool hasTriggered = false;

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
            if (hasTriggered) return;

            // Verify the collider belongs to the Player
            bool isPlayer = other.CompareTag("Player") ||
                            other.GetComponent<PlayerController>() != null ||
                            other.GetComponentInParent<PlayerController>() != null ||
                            other.GetComponent<CharacterController>() != null;

            if (!isPlayer) return;

            hasTriggered = true;
            Debug.Log($"<color=#5cb85c><b>[EscapeTrigger]</b></color> Player entered escape trigger volume '{gameObject.name}'.");

            if (GameManager.Instance != null)
            {
                GameManager.Instance.CompleteEscape();
            }
            else
            {
                var gm = FindAnyObjectByType<GameManager>();
                if (gm != null)
                {
                    gm.CompleteEscape();
                }
                else
                {
                    Debug.LogError("[EscapeTrigger] No GameManager found to report escape completion!");
                }
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = hasTriggered ? new Color(0f, 1f, 0f, 0.35f) : new Color(0f, 0.8f, 1f, 0.35f);
            var box = GetComponent<BoxCollider>();
            if (box != null)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.color = hasTriggered ? Color.green : Color.cyan;
                Gizmos.DrawWireCube(box.center, box.size);
            }
        }
    }
}
