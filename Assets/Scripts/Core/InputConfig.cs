// Centralized input configuration system for the Escape Room
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EscapeRoom.Core
{
    /// <summary>
    /// Centralized input configuration system for the Escape Room.
    /// Manages non-conflicting interaction keys:
    ///   • [I] = Interact / Examine / Inspect / Pickup
    ///   • [O] = Open (Lockbox, Door, Chest, Cabinet)
    ///   • [E] = Enter / Use / Activate (Keypad, Security Reader, Console)
    ///   • [Esc] = Close / Exit opened panels
    /// </summary>
    [DisallowMultipleComponent]
    public class InputConfig : MonoBehaviour
    {
        private static InputConfig instance;
        public static InputConfig Instance => instance;

        [SerializeField] private Key interactKey = Key.I;

        public event Action<Key> OnInteractKeyChanged;

        public Key InteractKey
        {
            get => interactKey;
            set => SetInteractKey(value);
        }

        public static Key CurrentInteractKey => instance != null ? instance.interactKey : Key.I;

        public static string InteractKeyName => "I";
        public static string OpenKeyName => "O";
        public static string UseKeyName => "E";
        public static string CloseKeyName => "Esc";

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

        public void SetInteractKey(Key newKey)
        {
            interactKey = newKey;
            OnInteractKeyChanged?.Invoke(newKey);
        }

        /// <summary>Checks if [I] (or configured interact key) was pressed this frame.</summary>
        public static bool IsInteractPressedThisFrame()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return false;
            return keyboard.iKey.wasPressedThisFrame || keyboard.fKey.wasPressedThisFrame;
        }

        /// <summary>Checks if [O] (Open) key was pressed this frame.</summary>
        public static bool IsOpenPressedThisFrame()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return false;
            return keyboard.oKey.wasPressedThisFrame;
        }

        /// <summary>Checks if [E] (Enter / Use / Activate) key was pressed this frame.</summary>
        public static bool IsUsePressedThisFrame()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return false;
            return keyboard.eKey.wasPressedThisFrame;
        }

        /// <summary>Checks if [Esc] (Close / Exit) key was pressed this frame.</summary>
        public static bool IsClosePressedThisFrame()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return false;
            return keyboard.escapeKey.wasPressedThisFrame;
        }

        /// <summary>
        /// Formats an action string into a key prompt based on action type.
        /// Examples:
        ///   "Examine Note" -> "Press I to Examine Note"
        ///   "Open Lockbox" -> "Press O to Open Lockbox"
        ///   "Use Keypad"   -> "Press E to Use Keypad"
        /// </summary>
        public static string FormatPrompt(string actionText)
        {
            if (string.IsNullOrEmpty(actionText)) return "Press I to Interact";

            // Strip existing prefixes
            string clean = actionText;
            if (clean.StartsWith("["))
            {
                int endIdx = clean.IndexOf("] ");
                if (endIdx >= 0) clean = clean.Substring(endIdx + 2);
            }
            if (clean.StartsWith("Press E to ")) clean = clean.Replace("Press E to ", "");
            if (clean.StartsWith("Press F to ")) clean = clean.Replace("Press F to ", "");
            if (clean.StartsWith("Press I to ")) clean = clean.Replace("Press I to ", "");
            if (clean.StartsWith("Press O to ")) clean = clean.Replace("Press O to ", "");

            // Categorize by action keyword
            string lower = clean.ToLowerInvariant();
            if (lower.Contains("open") || lower.Contains("unlock"))
            {
                return $"Press O to {clean}";
            }
            else if (lower.Contains("use") || lower.Contains("keypad") || lower.Contains("enter") || lower.Contains("activate") || lower.Contains("reader") || lower.Contains("terminal"))
            {
                return $"Press E to {clean}";
            }
            else
            {
                return $"Press I to {clean}";
            }
        }
    }
}
