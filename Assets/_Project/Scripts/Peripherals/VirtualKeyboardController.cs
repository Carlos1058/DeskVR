using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeskVR.Peripherals
{
    /// <summary>
    /// Coordinates all 3D keys on the physical virtual keyboard.
    /// Implements precise single-key targeting per pointer (rejecting multi-key overlapping),
    /// visual hover animation on touch, and Index Trigger (gatillo) confirmation to write.
    /// Features procedural audio click feedback and laser pointer color syncing.
    /// Completely immune to VR headset / camera proximity.
    /// </summary>
    public class VirtualKeyboardController : MonoBehaviour
    {
        public static event Action<string, KeyActionType> OnKeyTyped;
        public static event Action<VirtualKey> OnKeyPressed;

        [Header("Audio Feedback")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip keyClickSound;

        [Header("Targeting Constraints")]
        [Tooltip("Maximum distance from pointer tip to key center for contact detection (meters)")]
        [SerializeField] private float touchRadius = 0.018f;

        private static VirtualKeyboardController instance;
        public static VirtualKeyboardController Instance => instance;

        private readonly List<VirtualKey> allKeys = new List<VirtualKey>();
        private readonly Dictionary<string, VirtualKey> keyLookup = new Dictionary<string, VirtualKey>(StringComparer.OrdinalIgnoreCase);

        private readonly List<DeskVRPointerTip> activePointers = new List<DeskVRPointerTip>();
        private readonly HashSet<VirtualKey> targetedKeysThisFrame = new HashSet<VirtualKey>();

        // Procedural audio for key click (fallback when no AudioClip assigned)
        private AudioClip proceduralClickClip;

        private void Awake()
        {
            instance = this;
            RegisterChildKeys();

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 1f; // 3D sound
            }

            // Generate procedural key click sound (crisp, satisfying mechanical click)
            GenerateProceduralClickSound();
        }

        private void Start()
        {
            FindVRPointers();
        }

        /// <summary>
        /// Generates a short procedural mechanical key click AudioClip.
        /// Used as fallback when no keyClickSound asset is assigned.
        /// Produces a 30ms punchy click with subtle resonance.
        /// </summary>
        private void GenerateProceduralClickSound()
        {
            int sampleRate = 44100;
            float duration = 0.035f; // 35ms crisp click
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float normalizedT = (float)i / sampleCount;

                // Sharp attack envelope: fast onset, rapid exponential decay
                float envelope = Mathf.Exp(-normalizedT * 12f);

                // Primary click: mix of frequencies for a mechanical keyboard feel
                float click = Mathf.Sin(2f * Mathf.PI * 3200f * t) * 0.4f;    // High tick
                click += Mathf.Sin(2f * Mathf.PI * 1200f * t) * 0.3f;         // Mid body
                click += Mathf.Sin(2f * Mathf.PI * 650f * t) * 0.2f;          // Low thump

                // Add subtle noise burst for realism (first 5ms)
                if (t < 0.005f)
                {
                    click += (UnityEngine.Random.value * 2f - 1f) * 0.3f * (1f - t / 0.005f);
                }

                samples[i] = Mathf.Clamp(click * envelope * 0.75f, -1f, 1f);
            }

            proceduralClickClip = AudioClip.Create("ProceduralKeyClick", sampleCount, 1, sampleRate, false);
            proceduralClickClip.SetData(samples, 0);
        }

        public void RegisterChildKeys()
        {
            allKeys.Clear();
            keyLookup.Clear();
            var keys = GetComponentsInChildren<VirtualKey>(true);
            foreach (var key in keys)
            {
                allKeys.Add(key);

                if (!string.IsNullOrEmpty(key.DisplayLabel) && !keyLookup.ContainsKey(key.DisplayLabel))
                {
                    keyLookup[key.DisplayLabel] = key;
                }

                if (!string.IsNullOrEmpty(key.CharacterValue) && !keyLookup.ContainsKey(key.CharacterValue))
                {
                    keyLookup[key.CharacterValue] = key;
                }
            }
        }

        public void FindVRPointers()
        {
            activePointers.Clear();

            // Find all authorized DeskVRPointerTip components (created on left & right controllers)
            var taggedTips = FindObjectsByType<DeskVRPointerTip>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var tip in taggedTips)
            {
                // Absolute safeguard: Never evaluate headset / camera position
                if (tip.GetComponentInParent<Camera>() == null && !activePointers.Contains(tip))
                {
                    activePointers.Add(tip);
                }
            }
        }

        private void Update()
        {
            if (activePointers.Count == 0)
            {
                FindVRPointers();
                if (activePointers.Count == 0) return;
            }

            targetedKeysThisFrame.Clear();

            // Process each pointer (left hand, right hand)
            for (int p = 0; p < activePointers.Count; p++)
            {
                var pointer = activePointers[p];
                if (pointer == null || !pointer.gameObject.activeInHierarchy) continue;

                Vector3 tipPos = pointer.transform.position;

                // Find the SINGLE closest key to this pointer tip
                VirtualKey closestKey = null;
                float closestDistSq = float.MaxValue;

                for (int i = 0; i < allKeys.Count; i++)
                {
                    var key = allKeys[i];
                    if (key == null) continue;

                    Vector3 localTip = key.transform.InverseTransformPoint(tipPos);

                    // Precise footprint: half-extents are [-0.5, 0.5] along X and Z
                    bool insideFootprint = Mathf.Abs(localTip.x) <= 0.55f && Mathf.Abs(localTip.z) <= 0.55f;
                    // Contact height window: key top is at Y = 0.5; depress contact is [-0.40, 0.65]
                    bool withinPressHeight = localTip.y >= -0.40f && localTip.y <= 0.65f;

                    float distSq = (tipPos - key.transform.position).sqrMagnitude;

                    if ((insideFootprint && withinPressHeight) || (distSq < touchRadius * touchRadius))
                    {
                        if (distSq < closestDistSq)
                        {
                            closestDistSq = distSq;
                            closestKey = key;
                        }
                    }
                }

                // Update laser color based on targeting
                pointer.SetHoveringKey(closestKey != null);

                // If a key is targeted by this pointer:
                if (closestKey != null)
                {
                    targetedKeysThisFrame.Add(closestKey);
                    closestKey.SetHovered(true);

                    // Confirm keystroke ONLY when index trigger (gatillo) is pressed
                    if (pointer.WasTriggerPressedThisFrame())
                    {
                        closestKey.ConfirmPress(pointer);
                    }
                }
            }

            // Un-hover any key that is not targeted by any pointer this frame
            for (int i = 0; i < allKeys.Count; i++)
            {
                var key = allKeys[i];
                if (key != null && key.IsHovered && !targetedKeysThisFrame.Contains(key))
                {
                    key.SetHovered(false);
                }
            }
        }

        public static void TriggerKeyPressed(VirtualKey key)
        {
            if (instance != null && instance.audioSource != null)
            {
                // Use assigned AudioClip if available, otherwise use procedural click
                AudioClip clipToPlay = instance.keyClickSound ?? instance.proceduralClickClip;
                if (clipToPlay != null)
                {
                    // Slight pitch variation for natural feel (±8%)
                    instance.audioSource.pitch = UnityEngine.Random.Range(0.92f, 1.08f);
                    instance.audioSource.PlayOneShot(clipToPlay, 0.75f);
                }
            }

            OnKeyPressed?.Invoke(key);
            OnKeyTyped?.Invoke(key.CharacterValue, key.ActionType);
        }

        public void HighlightKey(string labelOrChar, bool highlight)
        {
            if (keyLookup.TryGetValue(labelOrChar, out var key))
            {
                key.SetHighlight(highlight);
                return;
            }

            // Fuzzy lookup if exact match not found (e.g. "Borrar" matching "Borrar ⌫")
            foreach (var kvp in keyLookup)
            {
                if (kvp.Key.StartsWith(labelOrChar, StringComparison.OrdinalIgnoreCase) ||
                    labelOrChar.StartsWith(kvp.Key, StringComparison.OrdinalIgnoreCase))
                {
                    kvp.Value.SetHighlight(highlight);
                    return;
                }
            }
        }

        public void ClearAllHighlights()
        {
            foreach (var kvp in keyLookup)
            {
                kvp.Value.SetHighlight(false);
            }
        }
    }
}
