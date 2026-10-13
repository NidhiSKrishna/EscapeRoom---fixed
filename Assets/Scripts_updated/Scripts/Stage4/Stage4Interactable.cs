using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Stage 4 - anything the player can press E on (buttons, panels, terminals, tiles, door...).
/// The player looks at it, sees the prompt text, presses E, and Interact() runs.
///
/// MAIN PROJECT NOTE: when the real interaction system arrives, this component can be replaced
/// by the project's own interactable. Stage 4 scripts only call Interact() / listen to OnInteracted.
/// </summary>
public class Stage4Interactable : MonoBehaviour, EscapeRoom.Interaction.IInteractable
{
    [SerializeField] private string promptText = "Press E to interact";
    [SerializeField] private bool interactable = true;
    [SerializeField] private UnityEvent onInteract = new UnityEvent();

    /// <summary>Code can subscribe to this instead of using the Inspector event.</summary>
    public event Action OnInteracted;

    public string PromptText { get { return promptText; } }
    public string InteractionPrompt => promptText;
    public bool CanInteract { get { return interactable; } }

    public void SetPrompt(string text)
    {
        promptText = text;
    }

    public void SetInteractable(bool value)
    {
        interactable = value;
    }

    public void Interact()
    {
        if (!interactable) return;
        if (OnInteracted != null) OnInteracted();
        if (onInteract != null) onInteract.Invoke();
    }
}
