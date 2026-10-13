using UnityEngine;

namespace EscapeRoom.Player
{
    /// <summary>
    /// Flashlight feature disabled per project requirement.
    /// Preserved as a stub component to prevent missing component or missing script errors in existing scene files.
    /// </summary>
    [DisallowMultipleComponent]
    public class FlashlightController : MonoBehaviour
    {
        private static FlashlightController instance;
        public static FlashlightController Instance => instance;

        public bool IsOn => false;
        public float BatteryFraction => 1.0f;
        public float CurrentBattery => 300f;
        public float MaxBatterySeconds => 300f;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }
            instance = this;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        public void EnsureSpotlightSetup() { }
        public bool CanToggleFlashlight() => false;
        public void ToggleFlashlight() { }
        public void AddBatteryTime(float seconds) { }
        public void ResetBattery() { }
    }
}
