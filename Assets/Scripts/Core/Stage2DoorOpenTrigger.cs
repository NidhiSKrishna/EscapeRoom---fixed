using UnityEngine;
using EscapeRoom.UI;
using EscapeRoom.Puzzle;

namespace EscapeRoom.Core
{
    /// <summary>
    /// Simple trigger helper that listens for the security door opening
    /// and then shows the Stage 2 Complete UI.
    /// Attach to the same GameObject as the DoorController.
    /// </summary>
    [DisallowMultipleComponent]
    public class Stage2DoorOpenTrigger : MonoBehaviour
    {
        [SerializeField] private Stage2CompleteUI stage2UI;
        [SerializeField] private bool hasTriggered = false;

        private DoorController door;

        private void Awake()
        {
            door = GetComponent<DoorController>();
        }

        private void Update()
        {
            if (hasTriggered) return;
            if (door == null) return;
            if (door.IsOpen)
            {
                hasTriggered = true;
                TriggerCompletion();
            }
        }

        private void TriggerCompletion()
        {
            var ui = stage2UI;
            if (ui == null) ui = FindAnyObjectByType<Stage2CompleteUI>();
            if (ui != null)
            {
                ui.ShowStage2Complete();
            }
            else
            {
                EscapeRoom.UI.FeedbackHUD.ShowMessage(
                    "STAGE 2 COMPLETE  ·  Security Corridor Unlocked",
                    new Color(0.25f, 0.95f, 1.0f));
            }
            Debug.Log("<color=#5cb85c><b>[Stage2DoorOpenTrigger]</b></color> Stage 2 door opened — completion triggered.");
        }
    }
}
