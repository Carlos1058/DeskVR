using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeskVR.Peripherals
{
    /// <summary>
    /// Coordinates all 3D keys on the physical virtual keyboard.
    /// Implements precise single-key direct touch interaction (touch-to-type) per controller pointer.
    /// Features advanced anti-chatter / hysteresis protection:
    /// - Dual-threshold Schmitt trigger (press at y <= 0.45, release at y >= 0.65) to eliminate hand tremors
    /// - Per-key release latch: a pressed key CANNOT trigger again until the hand visibly lifts up
    /// - Cooldown timers (250ms same-key anti-bounce, 80ms inter-key fluid typing)
    /// - Completely independent per hand
    /// - 100% immune to VR headset / camera proximity
    /// </summary>
    public class VirtualKeyboardController : MonoBehaviour
    {
        public static event Action<string, KeyActionType> OnKeyTyped;
        public static event Action<VirtualKey> OnKeyPressed;

        [Header("Audio Feedback")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip keyClickSound;

        [Header("Anti-Chatter & Hysteresis Settings")]
        [Tooltip("Minimum time (seconds) before the SAME key can fire again on the same hand")]
        [SerializeField] private float sameKeyCooldown = 0.25f; // 250ms delay to eliminate accidental repeat taps

        [Tooltip("Minimum time (seconds) between pressing DIFFERENT keys on the same hand")]
        [SerializeField] private float interKeyCooldown = 0.08f; // 80ms for fluid cadence between letters

        [Tooltip("Height threshold for press activation (key local Y, key resting surface is at 0.50)")]
        [SerializeField] private float pressHeightThreshold = 0.45f;

        [Tooltip("Height threshold for key release (key local Y, user must lift above this to re-arm)")]
        [SerializeField] private float releaseHeightThreshold = 0.65f;

        private static VirtualKeyboardController instance;
        public static VirtualKeyboardController Instance => instance;

        private readonly List<VirtualKey> allKeys = new List<VirtualKey>();
        private readonly Dictionary<string, VirtualKey> keyLookup = new Dictionary<string, VirtualKey>(StringComparer.OrdinalIgnoreCase);

        private readonly List<DeskVRPointerTip> activePointers = new List<DeskVRPointerTip>();
        private readonly HashSet<VirtualKey> depressedKeysThisFrame = new HashSet<VirtualKey>();

        // Per-pointer press & latch tracking
        private readonly Dictionary<DeskVRPointerTip, VirtualKey> pointerCurrentKey = new Dictionary<DeskVRPointerTip, VirtualKey>();
        private readonly Dictionary<DeskVRPointerTip, VirtualKey> pointerLatchedKey = new Dictionary<DeskVRPointerTip, VirtualKey>();
        private readonly Dictionary<DeskVRPointerTip, float> pointerLastTypeTime = new Dictionary<DeskVRPointerTip, float>();
        private readonly Dictionary<VirtualKey, float> keyLastPressTime = new Dictionary<VirtualKey, float>();

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
            var taggedTips = FindObjectsByType<DeskVRPointerTip>(FindObjectsInactive.Exclude);
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

            depressedKeysThisFrame.Clear();

            // Process each pointer (left hand, right hand)
            for (int p = 0; p < activePointers.Count; p++)
            {
                var pointer = activePointers[p];
                if (pointer == null || !pointer.gameObject.activeInHierarchy) continue;

                Vector3 tipPos = pointer.transform.position;

                // 1. Check if previously latched key can be unlatched (user lifted hand above release height or moved away)
                if (pointerLatchedKey.TryGetValue(pointer, out VirtualKey latchedKey) && latchedKey != null)
                {
                    Vector3 latchedLocal = latchedKey.transform.InverseTransformPoint(tipPos);
                    bool outsideFootprint = Mathf.Abs(latchedLocal.x) > 0.60f || Mathf.Abs(latchedLocal.z) > 0.60f;
                    bool liftedAboveRelease = latchedLocal.y > releaseHeightThreshold;

                    if (liftedAboveRelease || outsideFootprint)
                    {
                        // User has clearly lifted or moved away: unlatch so key can be typed again
                        pointerLatchedKey[pointer] = null;
                        latchedKey = null;
                    }
                }

                // 2. Find the SINGLE closest key to this pointer tip
                VirtualKey closestKey = null;
                float closestDistSq = float.MaxValue;
                bool isWithinPressZone = false;

                for (int i = 0; i < allKeys.Count; i++)
                {
                    var key = allKeys[i];
                    if (key == null) continue;

                    Vector3 localTip = key.transform.InverseTransformPoint(tipPos);

                    // Horizontal footprint check
                    bool insideFootprint = Mathf.Abs(localTip.x) <= 0.52f && Mathf.Abs(localTip.z) <= 0.52f;
                    // Contact height window (from below key up to release threshold)
                    bool insideContactHeight = localTip.y >= -0.80f && localTip.y <= releaseHeightThreshold;

                    if (insideFootprint && insideContactHeight)
                    {
                        float distSq = (tipPos - key.transform.position).sqrMagnitude;
                        if (distSq < closestDistSq)
                        {
                            closestDistSq = distSq;
                            closestKey = key;
                            // Tip has pushed down into the press threshold (below resting surface 0.50)
                            isWithinPressZone = localTip.y <= pressHeightThreshold;
                        }
                    }
                }

                // Update laser visual state
                pointer.SetHoveringKey(closestKey != null);

                // 3. Process key press with anti-chatter hysteresis & latching
                pointerLatchedKey.TryGetValue(pointer, out latchedKey);
                pointerLastTypeTime.TryGetValue(pointer, out float lastPointerTime);

                if (closestKey != null)
                {
                    pointerCurrentKey[pointer] = closestKey;

                    // Physically depress key if in press zone or latched
                    if (isWithinPressZone || latchedKey == closestKey)
                    {
                        depressedKeysThisFrame.Add(closestKey);
                    }

                    // Trigger key ONLY if:
                    // a) Tip has penetrated into the press zone (y <= pressHeightThreshold)
                    // b) This key is NOT already latched on this pointer (must lift up to re-arm)
                    if (isWithinPressZone && latchedKey != closestKey)
                    {
                        keyLastPressTime.TryGetValue(closestKey, out float lastKeyTime);

                        bool cooldownPassed = (Time.time - lastPointerTime >= interKeyCooldown) &&
                                              (Time.time - lastKeyTime >= sameKeyCooldown);

                        if (cooldownPassed)
                        {
                            // Latch immediately to lock out any tremors or micro-bounces
                            pointerLatchedKey[pointer] = closestKey;
                            pointerLastTypeTime[pointer] = Time.time;
                            keyLastPressTime[closestKey] = Time.time;

                            closestKey.Press(pointer);
                        }
                    }
                }
                else
                {
                    // Pointer is in the air: clear contact state
                    pointerCurrentKey[pointer] = null;
                    pointerLatchedKey[pointer] = null;
                }
            }

            // 4. Update physical depression state for all keys
            for (int i = 0; i < allKeys.Count; i++)
            {
                var key = allKeys[i];
                if (key != null)
                {
                    key.SetDepressed(depressedKeysThisFrame.Contains(key));
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
            for (int i = 0; i < allKeys.Count; i++)
            {
                var key = allKeys[i];
                if (key != null) key.SetHighlight(false);
            }
        }
    }
}
