using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

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
    /// Depresses when pressed, triggers haptic feedback, and sends key events.
    /// </summary>
    public class VirtualKey : MonoBehaviour
    {
        [Header("Key Configuration")]
        [SerializeField] private KeyActionType actionType = KeyActionType.Character;
        [SerializeField] private string characterValue = "A";
        [SerializeField] private string displayLabel = "A";

        [Header("Physical Movement")]
        [SerializeField] private float pressDepth = 0.005f; // 5mm travel distance
        [SerializeField] private float returnSpeed = 15f;

        [Header("Visual Feedback")]
        [SerializeField] private MeshRenderer keyRenderer;
        [SerializeField] private Color normalColor = new Color(0.9f, 0.9f, 0.92f);
        [SerializeField] private Color pressedColor = new Color(0.6f, 0.75f, 0.95f);
        [SerializeField] private Color highlightColor = new Color(1.0f, 0.85f, 0.2f); // Golden glow for tutorials

        private Vector3 initialLocalPos;
        private Vector3 pressedLocalPos;
        private bool isPressed = false;
        private bool isHighlighted = false;
        private Material keyMaterialInstance;

        public KeyActionType ActionType => actionType;
        public string CharacterValue => characterValue;
        public string DisplayLabel => displayLabel;

        private void Awake()
        {
            initialLocalPos = transform.localPosition;
            pressedLocalPos = initialLocalPos - new Vector3(0f, pressDepth, 0f);

            if (keyRenderer == null) keyRenderer = GetComponent<MeshRenderer>();
            if (keyRenderer != null)
            {
                keyMaterialInstance = keyRenderer.material; // Unique instance for color transitions
                keyMaterialInstance.color = normalColor;
            }
        }

        public void Configure(KeyActionType type, string value, string label)
        {
            actionType = type;
            characterValue = value;
            displayLabel = label;
        }

        private void Update()
        {
            // Smoothly return key to resting position when released
            if (!isPressed && transform.localPosition != initialLocalPos)
            {
                transform.localPosition = Vector3.Lerp(transform.localPosition, initialLocalPos, Time.deltaTime * returnSpeed);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (isPressed) return;

            // Triggered by XR controller tip or hand collider
            Press(other);
        }

        private void OnTriggerExit(Collider other)
        {
            if (isPressed)
            {
                Release();
            }
        }

        public void Press(Collider triggeringCollider = null)
        {
            isPressed = true;
            transform.localPosition = pressedLocalPos;

            if (keyMaterialInstance != null)
            {
                keyMaterialInstance.color = pressedColor;
            }

            // Haptic feedback to controller if applicable
            if (triggeringCollider != null)
            {
                var interactor = triggeringCollider.GetComponentInParent<XRBaseInputInteractor>();
                if (interactor != null)
                {
                    interactor.SendHapticImpulse(0.5f, 0.04f);
                }
            }

            // Notify keyboard manager
            VirtualKeyboardController.TriggerKeyPressed(this);
        }

        public void Release()
        {
            isPressed = false;
            if (keyMaterialInstance != null)
            {
                keyMaterialInstance.color = isHighlighted ? highlightColor : normalColor;
            }
        }

        public void SetHighlight(bool highlight)
        {
            isHighlighted = highlight;
            if (keyMaterialInstance != null && !isPressed)
            {
                keyMaterialInstance.color = highlight ? highlightColor : normalColor;
            }
        }
    }
}
