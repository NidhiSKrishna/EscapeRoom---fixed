using UnityEngine;

namespace EscapeRoom.Core
{
    /// <summary>
    /// ScriptableObject defining a single objective step in the guided escape room sequence.
    /// Configurable in inspector without code changes.
    /// </summary>
    [CreateAssetMenu(fileName = "NewObjectiveStep", menuName = "Escape Room/Objective Step")]
    public class ObjectiveStep : ScriptableObject
    {
        [Tooltip("Short display title or identifier for this objective.")]
        [SerializeField] private string stepTitle = "Objective";

        [TextArea(2, 4)]
        [Tooltip("Actionable player instruction displayed on the HUD and expanded view.")]
        [SerializeField] private string instruction = "Explore your surroundings.";

        [Tooltip("The unique event flag required to complete this step.")]
        [SerializeField] private string completionFlag = "STEP_FLAG";

        [Tooltip("Optional room/zone identifier (e.g. Attic, Storage Room, Cellar).")]
        [SerializeField] private string roomIdentifier = "Attic";

        public string StepTitle
        {
            get => stepTitle;
            set => stepTitle = value;
        }

        public string Instruction
        {
            get => instruction;
            set => instruction = value;
        }

        public string CompletionFlag
        {
            get => completionFlag;
            set => completionFlag = value;
        }

        public string RoomIdentifier
        {
            get => roomIdentifier;
            set => roomIdentifier = value;
        }

        public void Configure(string title, string instr, string flag, string room = "Attic")
        {
            stepTitle = title;
            instruction = instr;
            completionFlag = flag;
            roomIdentifier = room;
        }
    }
}
