using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace DeskVR.Peripherals
{
    /// <summary>
    /// Coordinates all 3D keys on the physical virtual keyboard.
    /// Tracks VR controller proximity and pointer tips so typing registers seamlessly
    /// both via direct physical contact (finger / controller tip) and trigger colliders.
    /// Emits typing events for active input fields on the screen and manages key highlights.
    /// </summary>
    public class VirtualKeyboardController : MonoBehaviour
    {
        public static event Action<string, KeyActionType> OnKeyTyped;
        public static event Action<VirtualKey> OnKeyPressed;

        [Header("Audio Feedback")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip keyClickSound;

        [Header("Touch Settings")]
        [Tooltip("Fallback distance in meters to trigger key press")]
        [SerializeField] private float sphericalTouchRadius = 0.028f;

        private static VirtualKeyboardController instance;
        private readonly List<VirtualKey> allKeys = new List<VirtualKey>();
        private readonly Dictionary<string, VirtualKey> keyLookup = new Dictionary<string, VirtualKey>(StringComparer.OrdinalIgnoreCase);

        private readonly List<Transform> activePointers = new List<Transform>();
        private readonly HashSet<VirtualKey> keysTouchedThisFrame = new HashSet<VirtualKey>();

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
        }

        private void Start()
        {
            FindVRPointers();
        }

        private void RegisterChildKeys()
        {
            allKeys.Clear();
            keyLookup.Clear();
            var keys = GetComponentsInChildren<VirtualKey>();
            foreach (var key in keys)
            {
                allKeys.Add(key);
                if (!string.IsNullOrEmpty(key.DisplayLabel) && !keyLookup.ContainsKey(key.DisplayLabel))
                {
                    keyLookup[key.DisplayLabel] = key;
                }
            }
        }

        private void FindVRPointers()
        {
            activePointers.Clear();

            // 1. First priority: Dedicated pointer tips created by DeskVR
            var allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var t in allTransforms)
            {
                if (t.name == "DeskVR_PointerTip" && !activePointers.Contains(t))
                {
                    activePointers.Add(t);
                }
            }

            // 2. XR Controller Interactors
            var interactors = FindObjectsByType<XRBaseInputInteractor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var interactor in interactors)
            {
                if (interactor.gameObject.activeInHierarchy && !activePointers.Contains(interactor.transform))
                {
                    activePointers.Add(interactor.transform);
                }
            }

            // 3. Fallback: Search for controller objects by name
            if (activePointers.Count == 0)
            {
                foreach (var t in allTransforms)
                {
                    string n = t.name.ToLower();
                    if ((n.Contains("controller") || n.Contains("hand")) && t.GetComponent<Camera>() == null)
                    {
                        if (!activePointers.Contains(t)) activePointers.Add(t);
                    }
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

            keysTouchedThisFrame.Clear();

            // Test each pointer against each key
            for (int p = 0; p < activePointers.Count; p++)
            {
                var pointer = activePointers[p];
                if (pointer == null || !pointer.gameObject.activeInHierarchy) continue;

                Vector3 tipPos = pointer.position;

                // If pointer is a raw controller rather than a pointer tip child, project to controller front tip
                if (!pointer.name.Equals("DeskVR_PointerTip", StringComparison.OrdinalIgnoreCase))
                {
                    tipPos = pointer.TransformPoint(new Vector3(0f, -0.015f, 0.085f));
                }

                for (int i = 0; i < allKeys.Count; i++)
                {
                    var key = allKeys[i];
                    if (key == null) continue;

                    // Evaluate in the key's local coordinate space
                    Vector3 localTip = key.transform.InverseTransformPoint(tipPos);

                    // Key bounds: [-0.5, 0.5] along X and Z
                    bool insideFootprint = Mathf.Abs(localTip.x) <= 0.65f && Mathf.Abs(localTip.z) <= 0.65f;
                    // Key top surface is at Y = 0.5; depress contact is [-0.60, 0.70]
                    bool withinPressHeight = localTip.y >= -0.60f && localTip.y <= 0.70f;

                    float dist = Vector3.Distance(tipPos, key.transform.position);

                    if ((insideFootprint && withinPressHeight) || (dist < sphericalTouchRadius))
                    {
                        keysTouchedThisFrame.Add(key);
                        if (!key.IsPressed)
                        {
                            key.Press(pointer);
                        }
                    }
                }
            }

            // Release any key that is no longer being touched
            for (int i = 0; i < allKeys.Count; i++)
            {
                var key = allKeys[i];
                if (key != null && key.IsPressed && !keysTouchedThisFrame.Contains(key))
                {
                    key.Release();
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

        public void HighlightKey(string label, bool highlight)
        {
            if (keyLookup.TryGetValue(label, out var key))
            {
                key.SetHighlight(highlight);
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
