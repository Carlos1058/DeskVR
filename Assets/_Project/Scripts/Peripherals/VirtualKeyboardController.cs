using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeskVR.Peripherals
{
    /// <summary>
    /// Coordinates all 3D keys on the physical virtual keyboard.
    /// Emits typing events for active input fields on the screen and manages key highlights.
    /// </summary>
    public class VirtualKeyboardController : MonoBehaviour
    {
        public static event Action<string, KeyActionType> OnKeyTyped;
        public static event Action<VirtualKey> OnKeyPressed;

        [Header("Audio Feedback")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip keyClickSound;

        private static VirtualKeyboardController instance;
        private readonly Dictionary<string, VirtualKey> keyLookup = new Dictionary<string, VirtualKey>(StringComparer.OrdinalIgnoreCase);

        private void Awake()
        {
            instance = this;
            RegisterChildKeys();
        }

        private void RegisterChildKeys()
        {
            keyLookup.Clear();
            var keys = GetComponentsInChildren<VirtualKey>();
            foreach (var key in keys)
            {
                if (!string.IsNullOrEmpty(key.DisplayLabel) && !keyLookup.ContainsKey(key.DisplayLabel))
                {
                    keyLookup[key.DisplayLabel] = key;
                }
            }
        }

        public static void TriggerKeyPressed(VirtualKey key)
        {
            if (instance != null && instance.audioSource != null && instance.keyClickSound != null)
            {
                instance.audioSource.PlayOneShot(instance.keyClickSound, 0.6f);
            }

            OnKeyPressed?.Invoke(key);
            OnKeyTyped?.Invoke(key.CharacterValue, key.ActionType);
        }

        /// <summary>
        /// Highlights a specific key on the keyboard for guided learning steps.
        /// </summary>
        public void HighlightKey(string label, bool highlight)
        {
            if (keyLookup.TryGetValue(label, out var key))
            {
                key.SetHighlight(highlight);
            }
        }

        /// <summary>
        /// Clears highlighting on all keys.
        /// </summary>
        public void ClearAllHighlights()
        {
            foreach (var kvp in keyLookup)
            {
                kvp.Value.SetHighlight(false);
            }
        }
    }
}
