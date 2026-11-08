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
    /// 1. Physical hover/touch: Depresses visually to indicate targeting, but DOES NOT type.
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
        [SerializeField] private float returnSpeed = 18f;

        [Header("Visual Feedback Colors")]
        [SerializeField] private MeshRenderer keyRenderer;
        [SerializeField] private Color normalColor = new Color(0.95f, 0.95f, 0.97f);
        [SerializeField] private Color hoverColor = new Color(0.45f, 0.75f, 1.0f);     // Soft sky blue when pointer touches
        [SerializeField] private Color confirmedColor = new Color(0.10f, 0.45f, 1.0f); // Deep vibrant blue when trigger confirms
        [SerializeField] private Color highlightColor = new Color(1.0f, 0.78f, 0.08f); // Golden glow for tutorials

        private Vector3 initialLocalPos;
        private Vector3 pressedLocalPos;
        private bool isHovered = false;
        private bool isHighlighted = false;
        private Material keyMaterialInstance;
        private float confirmFlashTimer = 0f;

        public KeyActionType ActionType => actionType;
        public string CharacterValue => characterValue;
        public string DisplayLabel => displayLabel;
        public bool IsHovered => isHovered;
        public bool IsHighlighted => isHighlighted;

        private void Awake()
        {
            initialLocalPos = transform.localPosition;
            pressedLocalPos = initialLocalPos - new Vector3(0f, pressDepth, 0f);

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
            // Handle confirmation flash decay
            if (confirmFlashTimer > 0f)
            {
                confirmFlashTimer -= Time.deltaTime;
                if (confirmFlashTimer <= 0f)
                {
                    UpdateVisualColor();
                }
            }

            // Smooth physical return when not hovered
            if (!isHovered && transform.localPosition != initialLocalPos)
            {
                transform.localPosition = Vector3.Lerp(transform.localPosition, initialLocalPos, Time.deltaTime * returnSpeed);
            }
        }

        /// <summary>
        /// Called when an authorized pointer tip hovers over this key.
        /// Physically depresses the key and updates hover visuals, but DOES NOT type.
        /// </summary>
        public void SetHovered(bool hovered)
        {
            if (isHovered == hovered) return;

            isHovered = hovered;

            if (isHovered)
            {
                transform.localPosition = pressedLocalPos;
            }

            UpdateVisualColor();
        }

        /// <summary>
        /// Confirms the keystroke when the user presses the Index Trigger on the controller.
        /// Emits the character event, sends haptics, and flashes confirmed visual feedback.
        /// </summary>
        public void ConfirmPress(DeskVRPointerTip pointerTip)
        {
            transform.localPosition = pressedLocalPos;
            confirmFlashTimer = 0.15f;

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
