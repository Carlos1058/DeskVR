using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace DeskVR.Peripherals
{
    /// <summary>
    /// Controls the physical 3D mouse on the virtual desk.
    /// Constrains movement to the desk surface, calculates 2D deltas for the simulated OS,
    /// and triggers haptic pulses on user interactions (clicks, grab).
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public class VirtualMouseController : MonoBehaviour
    {
        [Header("Desk Bounds & Constraints")]
        [Tooltip("Transform of the desk or mousepad used as surface reference")]
        [SerializeField] private Transform deskSurface;

        [Tooltip("Width (X) and Depth (Z) in meters for the allowed mouse area")]
        [SerializeField] private Vector2 deskAreaSize = new Vector2(0.40f, 0.30f);

        [Tooltip("Height offset so the mouse sits nicely on top of the surface")]
        [SerializeField] private float heightOffset = 0.015f;

        [Tooltip("Keep the mouse facing forward relative to the desk")]
        [SerializeField] private bool lockRotation = true;

        [Header("Sensitivity")]
        [Tooltip("Multiplier to convert physical 3D movement to 2D screen pixels")]
        [SerializeField] private float mouseSensitivity = 1400f;

        [Header("Haptic Feedback Settings")]
        [Range(0f, 1f)]
        [SerializeField] private float grabHapticAmplitude = 0.25f;
        [SerializeField] private float grabHapticDuration = 0.05f;

        [Range(0f, 1f)]
        [SerializeField] private float clickHapticAmplitude = 0.65f;
        [SerializeField] private float clickHapticDuration = 0.06f;

        // Static events for the simulated OS and Curriculum modules
        public static event Action<Vector2> OnMouseMoved;
        public static event Action OnLeftClickDown;
        public static event Action OnLeftClickUp;
        public static event Action OnLeftClick;
        public static event Action OnRightClick;
        public static event Action<bool> OnGrabStateChanged;

        private XRGrabInteractable grabInteractable;
        private Vector3 lastPosition;
        private bool isHeld = false;
        private bool isLeftClickPressed = false;
        private IXRSelectInteractor currentInteractor;

        public bool IsHeld => isHeld;
        public bool IsLeftClickPressed => isLeftClickPressed;

        private void Awake()
        {
            grabInteractable = GetComponent<XRGrabInteractable>();
            if (grabInteractable != null)
            {
                grabInteractable.selectEntered.AddListener(OnGrabbed);
                grabInteractable.selectExited.AddListener(OnReleased);
                grabInteractable.activated.AddListener(OnActivated);
                grabInteractable.deactivated.AddListener(OnDeactivated);
            }
        }

        private void Start()
        {
            lastPosition = transform.position;
            if (deskSurface != null)
            {
                ClampToDesk();
            }
        }

        private void LateUpdate()
        {
            if (isHeld)
            {
                ClampToDesk();
                CalculateDelta();
            }
        }

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            isHeld = true;
            currentInteractor = args.interactorObject;
            lastPosition = transform.position;
            TriggerHaptic(grabHapticAmplitude, grabHapticDuration);
            OnGrabStateChanged?.Invoke(true);
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            isHeld = false;
            if (isLeftClickPressed)
            {
                OnDeactivated(null);
            }
            currentInteractor = null;
            ClampToDesk();
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

        private void ClampToDesk()
        {
            if (deskSurface == null) return;

            Vector3 currentPos = transform.position;
            Vector3 deskPos = deskSurface.position;

            currentPos.y = deskPos.y + heightOffset;

            float halfWidth = deskAreaSize.x * 0.5f;
            float halfDepth = deskAreaSize.y * 0.5f;

            currentPos.x = Mathf.Clamp(currentPos.x, deskPos.x - halfWidth, deskPos.x + halfWidth);
            currentPos.z = Mathf.Clamp(currentPos.z, deskPos.z - halfDepth, deskPos.z + halfDepth);

            transform.position = currentPos;

            if (lockRotation)
            {
                transform.rotation = Quaternion.Euler(0f, deskSurface.eulerAngles.y, 0f);
            }
        }

        private void CalculateDelta()
        {
            Vector3 currentPos = transform.position;
            Vector3 diff = currentPos - lastPosition;

            if (diff.sqrMagnitude > 0.0000005f)
            {
                Vector3 localDiff = deskSurface != null ? deskSurface.InverseTransformDirection(diff) : diff;
                Vector2 screenDelta = new Vector2(localDiff.x, localDiff.z) * mouseSensitivity;

                OnMouseMoved?.Invoke(screenDelta);
                lastPosition = currentPos;
            }
        }

        public void TriggerHaptic(float amplitude, float duration)
        {
            if (currentInteractor is XRBaseInputInteractor inputInteractor)
            {
                inputInteractor.SendHapticImpulse(amplitude, duration);
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
