using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace DeskVR.Peripherals
{
    /// <summary>
    /// Controls the physical 3D mouse on the virtual desk.
    /// Uses delta-based tracking relative to the VR controller so the mouse slides smoothly on the mousepad.
    /// Disables XRI automatic transform tracking so XRI doesn't fight desk plane constraints.
    /// Emits 2D movement and click events for the simulated OS.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public class VirtualMouseController : MonoBehaviour
    {
        [Header("Desk Bounds & Constraints")]
        [Tooltip("Transform of the mousepad used as center reference")]
        [SerializeField] private Transform deskSurface;

        [Tooltip("Width (X) and Depth (Z) in meters for the allowed mouse area")]
        [SerializeField] private Vector2 deskAreaSize = new Vector2(0.36f, 0.26f);

        [Tooltip("Height offset so the mouse sits nicely on top of the surface")]
        [SerializeField] private float heightOffset = 0.015f;

        [Tooltip("Keep the mouse facing forward relative to the desk")]
        [SerializeField] private bool lockRotation = true;

        [Header("Sensitivity")]
        [Tooltip("Multiplier to convert physical 3D displacement to 2D screen pixels")]
        [SerializeField] private float mouseSensitivity = 6500f;

        [Header("Haptic Feedback Settings")]
        [Range(0f, 1f)]
        [SerializeField] private float grabHapticAmplitude = 0.35f;
        [SerializeField] private float grabHapticDuration = 0.05f;

        [Range(0f, 1f)]
        [SerializeField] private float clickHapticAmplitude = 0.75f;
        [SerializeField] private float clickHapticDuration = 0.06f;

        // Static events for the simulated OS and Curriculum modules
        public static event Action<Vector2> OnMouseMoved;
        public static event Action OnLeftClickDown;
        public static event Action OnLeftClickUp;
        public static event Action OnLeftClick;
        public static event Action OnRightClick;
        public static event Action<bool> OnGrabStateChanged;

        private XRGrabInteractable grabInteractable;
        private bool isHeld = false;
        private bool isLeftClickPressed = false;
        private IXRSelectInteractor currentInteractor;
        private Transform activeControllerTransform;
        private Vector3 lastControllerPosition;

        public bool IsHeld => isHeld;
        public bool IsLeftClickPressed => isLeftClickPressed;

        private void Awake()
        {
            grabInteractable = GetComponent<XRGrabInteractable>();
            if (grabInteractable != null)
            {
                // Crucial: Tell XRI not to move or rotate the transform directly.
                // We handle constrained 2D movement on the desk surface manually.
                grabInteractable.trackPosition = false;
                grabInteractable.trackRotation = false;
                grabInteractable.throwOnDetach = false;

                grabInteractable.selectEntered.AddListener(OnGrabbed);
                grabInteractable.selectExited.AddListener(OnReleased);
                grabInteractable.activated.AddListener(OnActivated);
                grabInteractable.deactivated.AddListener(OnDeactivated);
            }
        }

        private void Start()
        {
            if (deskSurface != null)
            {
                ClampInitialPosition();
            }
        }

        private void ClampInitialPosition()
        {
            if (deskSurface == null) return;
            Vector3 pos = transform.position;
            Vector3 deskPos = deskSurface.position;
            pos.y = deskPos.y + heightOffset;

            float halfWidth = deskAreaSize.x * 0.5f;
            float halfDepth = deskAreaSize.y * 0.5f;
            pos.x = Mathf.Clamp(pos.x, deskPos.x - halfWidth, deskPos.x + halfWidth);
            pos.z = Mathf.Clamp(pos.z, deskPos.z - halfDepth, deskPos.z + halfDepth);

            transform.position = pos;
            if (lockRotation)
            {
                transform.rotation = Quaternion.Euler(0f, deskSurface.eulerAngles.y, 0f);
            }
        }

        private void Update()
        {
            if (isHeld && activeControllerTransform != null)
            {
                Vector3 currentControllerPos = activeControllerTransform.position;
                Vector3 controllerDelta = currentControllerPos - lastControllerPosition;
                lastControllerPosition = currentControllerPos;

                // Move mouse along the desk X/Z plane according to controller displacement
                Vector3 currentMousePos = transform.position;
                Vector3 newMousePos = currentMousePos + new Vector3(controllerDelta.x, 0f, controllerDelta.z);

                // Clamp within mousepad surface bounds
                if (deskSurface != null)
                {
                    Vector3 deskPos = deskSurface.position;
                    float halfWidth = deskAreaSize.x * 0.5f;
                    float halfDepth = deskAreaSize.y * 0.5f;

                    newMousePos.y = deskPos.y + heightOffset;
                    newMousePos.x = Mathf.Clamp(newMousePos.x, deskPos.x - halfWidth, deskPos.x + halfWidth);
                    newMousePos.z = Mathf.Clamp(newMousePos.z, deskPos.z - halfDepth, deskPos.z + halfDepth);
                }

                Vector3 actualDisplacement = newMousePos - currentMousePos;
                transform.position = newMousePos;

                if (lockRotation && deskSurface != null)
                {
                    transform.rotation = Quaternion.Euler(0f, deskSurface.eulerAngles.y, 0f);
                }

                // If mouse moved, fire screen delta event
                if (actualDisplacement.sqrMagnitude > 0.0000001f)
                {
                    Vector2 screenDelta = new Vector2(actualDisplacement.x, actualDisplacement.z) * mouseSensitivity;
                    OnMouseMoved?.Invoke(screenDelta);
                }
            }
        }

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            isHeld = true;
            currentInteractor = args.interactorObject;
            activeControllerTransform = args.interactorObject.transform;
            lastControllerPosition = activeControllerTransform.position;

            TriggerHaptic(grabHapticAmplitude, grabHapticDuration);
            OnGrabStateChanged?.Invoke(true);
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            isHeld = false;
            activeControllerTransform = null;
            if (isLeftClickPressed)
            {
                OnDeactivated(null);
            }
            currentInteractor = null;
            OnGrabStateChanged?.Invoke(false);
        }

        private void OnActivated(ActivateEventArgs args)
        {
            isLeftClickPressed = true;
            TriggerHaptic(clickHapticAmplitude, clickHapticDuration);
            OnLeftClickDown?.Invoke();
            OnLeftClick?.Invoke();
        }

        private void OnDeactivated(DeactivateEventArgs args)
        {
            isLeftClickPressed = false;
            OnLeftClickUp?.Invoke();
        }

        public void PerformLeftClick()
        {
            TriggerHaptic(clickHapticAmplitude, clickHapticDuration);
            OnLeftClickDown?.Invoke();
            OnLeftClick?.Invoke();
            OnLeftClickUp?.Invoke();
        }

        public void PerformRightClick()
        {
            TriggerHaptic(clickHapticAmplitude * 0.85f, clickHapticDuration);
            OnRightClick?.Invoke();
        }

        public void TriggerHaptic(float amplitude, float duration)
        {
            if (currentInteractor is XRBaseInputInteractor inputInteractor)
            {
                inputInteractor.SendHapticImpulse(amplitude, duration);
            }
            else if (activeControllerTransform != null)
            {
                var input = activeControllerTransform.GetComponentInParent<XRBaseInputInteractor>();
                if (input != null) input.SendHapticImpulse(amplitude, duration);
            }
        }

        private void OnDestroy()
        {
            if (grabInteractable != null)
            {
                grabInteractable.selectEntered.RemoveListener(OnGrabbed);
                grabInteractable.selectExited.RemoveListener(OnReleased);
                grabInteractable.activated.RemoveListener(OnActivated);
                grabInteractable.deactivated.RemoveListener(OnDeactivated);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (deskSurface == null) return;
            Gizmos.color = Color.green;
            Vector3 center = deskSurface.position + Vector3.up * heightOffset;
            Gizmos.DrawWireCube(center, new Vector3(deskAreaSize.x, 0.01f, deskAreaSize.y));
        }
    }
}
