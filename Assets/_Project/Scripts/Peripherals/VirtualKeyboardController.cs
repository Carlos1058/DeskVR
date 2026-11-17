using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeskVR.Peripherals
{
    /// <summary>
    /// Coordinates all 3D keys on the physical virtual keyboard.
    /// Implements precise single-key direct touch interaction (touch-to-type) per controller pointer.
    /// Touching down on a key presses it and types the letter once; holding it down does not repeat.
    /// Lifting or moving to another key allows immediate typing again.
    /// Completely independent per hand, with debouncing and zero trigger requirement.
    /// Absolutely immune to VR headset / camera proximity.
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
        [SerializeField] private float touchRadius = 0.022f;

        [Tooltip("Minimum time (seconds) between consecutive key presses on the same pointer")]
        [SerializeField] private float debounceInterval = 0.08f;

        private static VirtualKeyboardController instance;
        public static VirtualKeyboardController Instance => instance;

        private readonly List<VirtualKey> allKeys = new List<VirtualKey>();
        private readonly Dictionary<string, VirtualKey> keyLookup = new Dictionary<string, VirtualKey>(StringComparer.OrdinalIgnoreCase);

        private readonly List<DeskVRPointerTip> activePointers = new List<DeskVRPointerTip>();
        private readonly HashSet<VirtualKey> targetedKeysThisFrame = new HashSet<VirtualKey>();

        // Per-pointer press tracking: which key is currently being touched down by each pointer
        private readonly Dictionary<DeskVRPointerTip, VirtualKey> pointerCurrentKey = new Dictionary<DeskVRPointerTip, VirtualKey>();
        private readonly Dictionary<DeskVRPointerTip, float> pointerLastTypeTime = new Dictionary<DeskVRPointerTip, float>();

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

            GenerateProceduralClickSound();
        }

        private void Start()
        {
            FindVRPointers();
        }

        private void GenerateProceduralClickSound()
        {
            int sampleRate = 44100;
            float duration = 0.035f;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float normalizedT = (float)i / sampleCount;

                float envelope = Mathf.Exp(-normalizedT * 12f);
                float click = Mathf.Sin(2f * Mathf.PI * 3200f * t) * 0.4f;
                click += Mathf.Sin(2f * Mathf.PI * 1200f * t) * 0.3f;
                click += Mathf.Sin(2f * Mathf.PI * 650f * t) * 0.2f;

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
                    // Contact height window: key top is at Y = 0.5; contact depress is [-0.35, 0.65]
                    bool withinPressHeight = localTip.y >= -0.35f && localTip.y <= 0.65f;

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

                // Update laser visual state
                pointer.SetHoveringKey(closestKey != null);

                // Get state for this specific pointer
                pointerCurrentKey.TryGetValue(pointer, out VirtualKey currentKeyForPointer);
                pointerLastTypeTime.TryGetValue(pointer, out float lastPressTime);

                if (closestKey != null)
                {
                    targetedKeysThisFrame.Add(closestKey);

                    // DIRECT TOUCH-TO-TYPE:
                    // Trigger key ONLY when pointer transitions into a new key
                    if (currentKeyForPointer != closestKey)
                    {
                        if (Time.time - lastPressTime >= debounceInterval)
                        {
                            pointerCurrentKey[pointer] = closestKey;
                            pointerLastTypeTime[pointer] = Time.time;
                            closestKey.Press(pointer);
                        }
                    }
                }
                else
                {
                    // Pointer lifted or exited key: clear held state so user can press again
                    if (currentKeyForPointer != null)
                    {
                        pointerCurrentKey[pointer] = null;
                    }
                }
            }

            // Update physical depression state for all keys
            for (int i = 0; i < allKeys.Count; i++)
            {
                var key = allKeys[i];
                if (key != null)
                {
                    key.SetDepressed(targetedKeysThisFrame.Contains(key));
                }
            }
        }

        public static void TriggerKeyPressed(VirtualKey key)
        {
            if (instance != null && instance.audioSource != null)
            {
                AudioClip clipToPlay = instance.keyClickSound ?? instance.proceduralClickClip;
                if (clipToPlay != null)
                {
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
