using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace DeskVR.Peripherals
{
    /// <summary>
    /// Explicit pointer tip attached to each VR controller.
    /// Provides accurate index-trigger (gatillo) detection and localized haptic feedback.
    /// Supports Unity Input System, XR Interaction Toolkit, and universal OpenXR / InputDevices.
    /// </summary>
    [DisallowMultipleComponent]
    public class DeskVRPointerTip : MonoBehaviour
    {
        [Tooltip("True for right controller / hand, false for left")]
        public bool isRightHand = true;

        [Tooltip("Visual pointer tip transform")]
        public Transform visualTip;

        private XRBaseInputInteractor cachedInteractor;
        private bool wasTriggerPressedLastFrame = false;

        private void Awake()
        {
            cachedInteractor = GetComponentInParent<XRBaseInputInteractor>();
        }

        /// <summary>
        /// Returns true while the index trigger (gatillo) on this controller is held down.
        /// </summary>
        public bool IsTriggerHeld()
        {
            // 1. Universal OpenXR / XRNode InputDevices (Works with Meta Quest 2 / Link / SteamVR)
            var node = isRightHand ? XRNode.RightHand : XRNode.LeftHand;
            var device = InputDevices.GetDeviceAtXRNode(node);
            if (device.isValid)
            {
                if (device.TryGetFeatureValue(CommonUsages.triggerButton, out bool btn) && btn)
                    return true;
                if (device.TryGetFeatureValue(CommonUsages.trigger, out float trigVal) && trigVal > 0.35f)
                    return true;
                if (device.TryGetFeatureValue(CommonUsages.primaryButton, out bool prim) && prim)
                    return true;
            }

            // 2. Unity New Input System: Check XRController devices
            try
            {
                foreach (var inputDev in UnityEngine.InputSystem.InputSystem.devices)
                {
                    if (inputDev is UnityEngine.InputSystem.XR.XRController ctrl)
                    {
                        string ctrlName = ctrl.name.ToLower();
                        bool matchesHand = isRightHand
                            ? (ctrlName.Contains("right"))
                            : (ctrlName.Contains("left"));

                        if (matchesHand)
                        {
                            var trigBtn = ctrl.GetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("triggerButton") ??
                                          ctrl.GetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("trigger") ??
                                          ctrl.GetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("activate");
                            if (trigBtn != null && trigBtn.isPressed)
                                return true;

                            var trigAxis = ctrl.GetChildControl<UnityEngine.InputSystem.Controls.AxisControl>("trigger");
                            if (trigAxis != null && trigAxis.ReadValue() > 0.35f)
                                return true;
                        }
                    }
                }
            }
            catch
            {
                // Fallback safe
            }

            // 3. Editor mouse click fallback for rapid testing without headset
            if (Application.isEditor && Input.GetMouseButton(0))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns true only on the first frame the index trigger was pressed.
        /// Prevents multiple unintended keystrokes while holding the trigger.
        /// </summary>
        public bool WasTriggerPressedThisFrame()
        {
            bool isHeld = IsTriggerHeld();
            bool justPressed = isHeld && !wasTriggerPressedLastFrame;
            wasTriggerPressedLastFrame = isHeld;
            return justPressed;
        }

        /// <summary>
        /// Sends haptic impulse to this specific controller.
        /// </summary>
        public void SendHapticImpulse(float amplitude, float duration)
        {
            if (cachedInteractor != null && cachedInteractor.GetComponentInParent<Camera>() == null)
            {
                cachedInteractor.SendHapticImpulse(amplitude, duration);
                return;
            }

            var node = isRightHand ? XRNode.RightHand : XRNode.LeftHand;
            var device = InputDevices.GetDeviceAtXRNode(node);
            if (device.isValid && device.TryGetHapticCapabilities(out var caps) && caps.supportsImpulse)
            {
                device.SendHapticImpulse(0u, amplitude, duration);
            }
        }
    }
}
