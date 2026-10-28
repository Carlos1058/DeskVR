using System;
using UnityEngine;
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
    /// Supports both physical triggers and direct proximity touching from VR controllers.
    /// </summary>
    public class VirtualKey : MonoBehaviour
    {
        [Header("Key Configuration")]
        [SerializeField] private KeyActionType actionType = KeyActionType.Character;
        [SerializeField] private string characterValue = "A";
        [SerializeField] private string displayLabel = "A";

        [Header("Physical Movement")]
        [SerializeField] private float pressDepth = 0.005f; // 5mm travel distance
        [SerializeField] private float returnSpeed = 16f;

        [Header("Visual Feedback")]
        [SerializeField] private MeshRenderer keyRenderer;
        [SerializeField] private Color normalColor = new Color(0.92f, 0.92f, 0.94f);
        [SerializeField] private Color pressedColor = new Color(0.35f, 0.65f, 1.0f); // Bright blue when active
        [SerializeField] private Color highlightColor = new Color(1.0f, 0.85f, 0.2f); // Golden glow for tutorials

        private Vector3 initialLocalPos;
        private Vector3 pressedLocalPos;
        private bool isPressed = false;
        private bool isHighlighted = false;
        private Material keyMaterialInstance;

        public KeyActionType ActionType => actionType;
        public string CharacterValue => characterValue;
        public string DisplayLabel => displayLabel;
        public bool IsPressed => isPressed;

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
            if (keyMaterialInstance != null && !isPressed)
            {
                keyMaterialInstance.color = normalColor;
            }
        }

        private void Update()
        {
            if (!isPressed && transform.localPosition != initialLocalPos)
            {
                transform.localPosition = Vector3.Lerp(transform.localPosition, initialLocalPos, Time.deltaTime * returnSpeed);
            }
        }

        public void Press(Transform triggeringTransform = null)
        {
            if (isPressed) return;

            isPressed = true;
            transform.localPosition = pressedLocalPos;

            if (keyMaterialInstance != null)
            {
                keyMaterialInstance.color = pressedColor;
            }

            // Send haptics if triggered by a controller
            if (triggeringTransform != null)
            {
                var interactor = triggeringTransform.GetComponentInParent<XRBaseInputInteractor>();
                if (interactor != null)
                {
                    interactor.SendHapticImpulse(0.7f, 0.05f);
                }
            }

            // Notify controller
            VirtualKeyboardController.TriggerKeyPressed(this);
        }

        public void Release()
        {
            if (!isPressed) return;

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

        private void OnTriggerEnter(Collider other)
        {
            Press(other.transform);
        }

        private void OnTriggerExit(Collider other)
        {
            Release();
        }
    }
}
