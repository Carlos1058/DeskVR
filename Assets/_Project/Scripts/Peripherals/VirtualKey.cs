using System;
using UnityEngine;

namespace DeskVR.Peripherals
{
    public enum KeyActionType
    {
        Character,
        Space,
        Backspace,
        Enter
    }

    /// <summary>
    /// Represents an individual physical 3D key on the virtual keyboard.
    /// Operates via direct physical touch: touching down on a key depresses it,
    /// triggers haptic and audio feedback, and types the character once per press.
    /// Immune to repeated accidental inputs and completely independent per hand.
    /// </summary>
    public class VirtualKey : MonoBehaviour
    {
        [Header("Key Configuration")]
        [SerializeField] private KeyActionType actionType = KeyActionType.Character;
        [SerializeField] private string characterValue = "A";
        [SerializeField] private string displayLabel = "A";

        [Header("Physical Movement")]
        [SerializeField] private float pressDepth = 0.005f; // 5mm travel distance
        [SerializeField] private float pressSpeed = 30f;    // Fast responsive press-down
        [SerializeField] private float returnSpeed = 16f;   // Smooth spring return

        [Header("Visual Feedback Colors")]
        [SerializeField] private MeshRenderer keyRenderer;
        [SerializeField] private Color normalColor = new Color(0.95f, 0.95f, 0.97f);
        [SerializeField] private Color hoverColor = new Color(0.40f, 0.72f, 1.0f);     // Sky blue when depressed
        [SerializeField] private Color confirmedColor = new Color(0.12f, 0.45f, 0.95f); // Vibrant blue on press trigger
        [SerializeField] private Color highlightColor = new Color(1.0f, 0.78f, 0.08f); // Golden glow for tutorials

        [Header("Bounce Juice")]
        [SerializeField] private float confirmBounceDepth = 0.0025f;
        [SerializeField] private float confirmBounceSpeed = 40f;

        private Vector3 initialLocalPos;
        private Vector3 pressedLocalPos;
        private Vector3 confirmBouncePos;
        private bool isDepressed = false;
        private bool isHighlighted = false;
        private Material keyMaterialInstance;
        private float confirmFlashTimer = 0f;

        // Bounce state
        private Vector3 targetPosition;
        private bool isConfirmBouncing = false;
        private float confirmBounceTimer = 0f;

        // Scale punch animation
        private Vector3 originalScale;
        private float scalePunchTimer = 0f;
        private const float ScalePunchDuration = 0.10f;
        private const float ScalePunchAmount = 0.06f; // 6% punch

        public KeyActionType ActionType => actionType;
        public string CharacterValue => characterValue;
        public string DisplayLabel => displayLabel;
        public bool IsDepressed => isDepressed;
        public bool IsHighlighted => isHighlighted;

        private void Awake()
        {
            initialLocalPos = transform.localPosition;
            pressedLocalPos = initialLocalPos - new Vector3(0f, pressDepth, 0f);
            confirmBouncePos = pressedLocalPos - new Vector3(0f, confirmBounceDepth, 0f);
            targetPosition = initialLocalPos;
            originalScale = transform.localScale;

            if (keyRenderer == null) keyRenderer = GetComponent<MeshRenderer>();
            if (keyRenderer != null)
            {
                keyMaterialInstance = keyRenderer.material;
                keyMaterialInstance.color = normalColor;
            }
        }

        private void Start()
        {
            initialLocalPos = transform.localPosition;
            pressedLocalPos = initialLocalPos - new Vector3(0f, pressDepth, 0f);
            confirmBouncePos = pressedLocalPos - new Vector3(0f, confirmBounceDepth, 0f);
            targetPosition = initialLocalPos;
            originalScale = transform.localScale;
        }

        public void Configure(KeyActionType type, string value, string label)
        {
            actionType = type;
            characterValue = value;
            displayLabel = label;
        }

        public void Configure(KeyActionType type, string value, string label, Color baseColor)
        {
            actionType = type;
            characterValue = value;
            displayLabel = label;
            normalColor = baseColor;
            if (keyMaterialInstance != null && !isDepressed)
            {
                keyMaterialInstance.color = isHighlighted ? highlightColor : normalColor;
            }
        }

        private void Update()
        {
            // --- Smooth position animation ---
            if (isConfirmBouncing)
            {
                confirmBounceTimer -= Time.deltaTime;
                if (confirmBounceTimer > 0f)
                {
                    float t = 1f - (confirmBounceTimer / 0.06f);
                    transform.localPosition = Vector3.Lerp(
                        transform.localPosition,
                        t < 0.5f ? confirmBouncePos : pressedLocalPos,
                        Time.deltaTime * confirmBounceSpeed
                    );
                }
                else
                {
                    isConfirmBouncing = false;
                    targetPosition = isDepressed ? pressedLocalPos : initialLocalPos;
                }
            }
            else
            {
                float speed = isDepressed ? pressSpeed : returnSpeed;
                transform.localPosition = Vector3.Lerp(transform.localPosition, targetPosition, Time.deltaTime * speed);
            }

            // --- Scale punch animation ---
            if (scalePunchTimer > 0f)
            {
                scalePunchTimer -= Time.deltaTime;
                float t = scalePunchTimer / ScalePunchDuration;
                float punchFactor = 1f + Mathf.Sin(t * Mathf.PI) * ScalePunchAmount;
                transform.localScale = originalScale * punchFactor;
            }
            else if (transform.localScale != originalScale)
            {
                transform.localScale = originalScale;
            }

            // Handle confirmation flash decay
            if (confirmFlashTimer > 0f)
            {
                confirmFlashTimer -= Time.deltaTime;
                if (confirmFlashTimer <= 0f)
                {
                    UpdateVisualColor();
                }
            }
        }

        /// <summary>
        /// Called directly when an authorized pointer tip presses this key.
        /// Emits the character event, sends haptic vibration to the controller,
        /// triggers physical bounce, and plays visual confirmation feedback.
        /// </summary>
        public void Press(DeskVRPointerTip pointerTip)
        {
            isDepressed = true;
            isConfirmBouncing = true;
            confirmBounceTimer = 0.06f;
            confirmFlashTimer = 0.16f;
            scalePunchTimer = ScalePunchDuration;

            if (keyMaterialInstance != null)
            {
                keyMaterialInstance.color = confirmedColor;
            }

            if (pointerTip != null)
            {
                pointerTip.SendHapticImpulse(0.75f, 0.05f);
            }

            // Emit the key typed event
            VirtualKeyboardController.TriggerKeyPressed(this);
        }

        /// <summary>
        /// Updates whether the key is held down by a pointer.
        /// </summary>
        public void SetDepressed(bool depressed)
        {
            if (isDepressed == depressed) return;

            isDepressed = depressed;
            targetPosition = isDepressed ? pressedLocalPos : initialLocalPos;
            UpdateVisualColor();
        }

        public void SetHighlight(bool highlight)
        {
            isHighlighted = highlight;
            UpdateVisualColor();
        }

        private void UpdateVisualColor()
        {
            if (keyMaterialInstance == null) return;

            if (confirmFlashTimer > 0f)
            {
                keyMaterialInstance.color = confirmedColor;
            }
            else if (isDepressed)
            {
                keyMaterialInstance.color = hoverColor;
            }
            else if (isHighlighted)
            {
                keyMaterialInstance.color = highlightColor;
            }
            else
            {
                keyMaterialInstance.color = normalColor;
            }
        }
    }
}
