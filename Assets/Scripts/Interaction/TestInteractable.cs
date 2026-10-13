using UnityEngine;

namespace EscapeRoom.Interaction
{
    /// <summary>
    /// Prototype interactable object for verifying the first-person interaction pipeline.
    /// Implements IInteractable, displays prompt, logs interaction success, and rotates 45 degrees.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public class TestInteractable : MonoBehaviour, IInteractable
    {
        [Header("Interactable Properties")]
        [Tooltip("Message displayed when looking at this interactable.")]
        [SerializeField] private string promptMessage = "Press E to interact";

        [Tooltip("Whether this object is currently receptive to player interaction.")]
        [SerializeField] private bool isInteractable = true;

        [Tooltip("Degrees to rotate around the Y axis upon interaction.")]
        [SerializeField] private float rotationStepDegrees = 45.0f;

        public string InteractionPrompt => promptMessage;
        public bool CanInteract => isInteractable;

        public void Interact()
        {
            if (!isInteractable) return;

            Debug.Log($"<color=#5cb85c><b>[TestInteractable]</b></color> Test interaction successful on '{gameObject.name}'!");

            // Rotate object by 45 degrees around Y axis
            transform.Rotate(Vector3.up, rotationStepDegrees, Space.World);
        }
    }
}
