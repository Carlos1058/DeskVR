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
    /// Supports a two-phase interaction model:
    /// 1. Physical hover/touch: Depresses visually with smooth animation, but DOES NOT type.
    /// 2. Index Trigger confirmation: Pressing the controller trigger confirms and writes the letter.
    /// </summary>
    public class VirtualKey : MonoBehaviour
    {
        [Header("Key Configuration")]
        [SerializeField] private KeyActionType actionType = KeyActionType.Character;
        [SerializeField] private string characterValue = "A";
        [SerializeField] private string displayLabel = "A";

        [Header("Physical Movement")]
        [SerializeField] private float pressDepth = 0.005f; // 5mm travel distance
        [SerializeField] private float pressSpeed = 28f;    // Smooth press-down speed
        [SerializeField] private float returnSpeed = 14f;   // Smooth return speed (slightly slower for satisfying feel)

        [Header("Visual Feedback Colors")]
        [SerializeField] private MeshRenderer keyRenderer;
        [SerializeField] private Color normalColor = new Color(0.95f, 0.95f, 0.97f);
        [SerializeField] private Color hoverColor = new Color(0.45f, 0.75f, 1.0f);     // Soft sky blue when pointer touches
        [SerializeField] private Color confirmedColor = new Color(0.10f, 0.45f, 1.0f); // Deep vibrant blue when trigger confirms
        [SerializeField] private Color highlightColor = new Color(1.0f, 0.78f, 0.08f); // Golden glow for tutorials

        [Header("Confirm Animation")]
        [SerializeField] private float confirmBounceDepth = 0.003f;  // Extra bounce on confirm
        [SerializeField] private float confirmBounceSpeed = 35f;

        private Vector3 initialLocalPos;
        private Vector3 pressedLocalPos;
        private Vector3 confirmBouncePos;
        private bool isHovered = false;
        private bool isHighlighted = false;
        private Material keyMaterialInstance;
        private float confirmFlashTimer = 0f;

        // Smooth animation state
        private Vector3 targetPosition;
        private float currentAnimSpeed;
        private bool isConfirmBouncing = false;
        private float confirmBounceTimer = 0f;

        // Scale punch animation
        private Vector3 originalScale;
        private float scalePunchTimer = 0f;
        private const float ScalePunchDuration = 0.12f;
        private const float ScalePunchAmount = 0.08f; // 8% scale pulse

        public KeyActionType ActionType => actionType;
        public string CharacterValue => characterValue;
        public string DisplayLabel => displayLabel;
        public bool IsHovered => isHovered;
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
            // Re-capture in Start in case builder configured position after Awake
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
            if (keyMaterialInstance != null && !isHovered)
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
                    // Bounce down then return
                    float t = 1f - (confirmBounceTimer / 0.08f);
                    if (t < 0.5f)
                    {
                        // Press deeper
                        transform.localPosition = Vector3.Lerp(transform.localPosition, confirmBouncePos, Time.deltaTime * confirmBounceSpeed);
                    }
                    else
                    {
                        // Return to pressed position
                        transform.localPosition = Vector3.Lerp(transform.localPosition, pressedLocalPos, Time.deltaTime * confirmBounceSpeed);
                    }
                }
                else
                {
                    isConfirmBouncing = false;
                    // After bounce, return based on hover state
                    targetPosition = isHovered ? pressedLocalPos : initialLocalPos;
                }
            }
            else
            {
                // Smooth lerp toward target position
                float speed = isHovered ? pressSpeed : returnSpeed;
                transform.localPosition = Vector3.Lerp(transform.localPosition, targetPosition, Time.deltaTime * speed);
            }

            // --- Scale punch animation ---
            if (scalePunchTimer > 0f)
            {
                scalePunchTimer -= Time.deltaTime;
                float t = scalePunchTimer / ScalePunchDuration;
                // Sine pulse: scale up then back
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
        /// Called when an authorized pointer tip hovers over this key.
        /// Smoothly depresses the key and updates hover visuals, but DOES NOT type.
        /// </summary>
        public void SetHovered(bool hovered)
        {
            if (isHovered == hovered) return;

            isHovered = hovered;

            if (isHovered)
            {
                targetPosition = pressedLocalPos;
            }
            else
            {
                targetPosition = initialLocalPos;
            }

            UpdateVisualColor();
        }

        /// <summary>
        /// Confirms the keystroke when the user presses the Index Trigger on the controller.
        /// Emits the character event, sends haptics, plays bounce animation, and flashes confirmed visual feedback.
        /// </summary>
        public void ConfirmPress(DeskVRPointerTip pointerTip)
        {
            // Trigger confirm bounce animation
            isConfirmBouncing = true;
            confirmBounceTimer = 0.08f;
            confirmFlashTimer = 0.18f;

            // Scale punch for juicy feedback
            scalePunchTimer = ScalePunchDuration;

            if (keyMaterialInstance != null)
            {
                keyMaterialInstance.color = confirmedColor;
            }

            if (pointerTip != null)
            {
                pointerTip.SendHapticImpulse(0.85f, 0.07f);
            }

            // Emit the key event
            VirtualKeyboardController.TriggerKeyPressed(this);
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
            else if (isHovered)
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
