namespace EscapeRoom.Interaction
{
    /// <summary>
    /// Contract for any world object that the first-person player can interact with.
    /// Exposes prompt text, readiness state, and the execution method.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// Prompt message displayed to the player when looking at this object.
        /// e.g., "Press E to interact", "Press E to inspect", "Press E to open".
        /// </summary>
        string InteractionPrompt { get; }

        /// <summary>
        /// Indicates whether this object can currently be interacted with.
        /// </summary>
        bool CanInteract { get; }

        /// <summary>
        /// Executes the interaction logic when the player triggers the interact action.
        /// </summary>
        void Interact();
    }
}
