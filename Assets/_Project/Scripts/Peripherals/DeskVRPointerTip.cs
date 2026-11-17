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
    /// Provides accurate index-trigger (gatillo) detection, localized haptic feedback,
    /// and a visual laser/line renderer from the controller tip for targeting guidance.
    /// Supports Unity Input System, XR Interaction Toolkit, and universal OpenXR / InputDevices.
    /// </summary>
    [DisallowMultipleComponent]
    public class DeskVRPointerTip : MonoBehaviour
    {
        [Tooltip("True for right controller / hand, false for left")]
        public bool isRightHand = true;

        [Tooltip("Visual pointer tip transform")]
        public Transform visualTip;

        [Header("Laser Line Settings")]
        [Tooltip("Enable the pointer laser line from controller tip")]
        public bool showLaser = true;

        [Tooltip("Length of the laser beam in meters")]
        public float laserLength = 0.08f;

        [Tooltip("Width of the laser line")]
        public float laserWidth = 0.0025f;

        [Tooltip("Laser color when idle (not hovering a key)")]
        public Color laserIdleColor = new Color(0.3f, 0.75f, 1.0f, 0.65f);

        [Tooltip("Laser color when hovering over a key")]
        public Color laserActiveColor = new Color(0.1f, 0.95f, 0.5f, 0.90f);

        private XRBaseInputInteractor cachedInteractor;
        private bool wasTriggerPressedLastFrame = false;
        private LineRenderer laserLine;
        private bool isHoveringKey = false;

        // Glow pulse state
        private float glowPulsePhase = 0f;

        private void Awake()
        {
            cachedInteractor = GetComponentInParent<XRBaseInputInteractor>();
            SetupLaserLine();
        }

        private void Update()
        {
            UpdateLaser();
        }

        /// <summary>
        /// Sets up the LineRenderer for the laser pointer effect.
        /// </summary>
        private void SetupLaserLine()
        {
            if (!showLaser) return;

            laserLine = GetComponent<LineRenderer>();
            if (laserLine == null)
            {
                laserLine = gameObject.AddComponent<LineRenderer>();
            }

            laserLine.positionCount = 2;
            laserLine.startWidth = laserWidth;
            laserLine.endWidth = laserWidth * 0.3f; // Tapers to a fine point
            laserLine.useWorldSpace = true;
            laserLine.receiveShadows = false;
            laserLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            laserLine.allowOcclusionWhenDynamic = false;
            laserLine.numCapVertices = 2;

            // Create a simple unlit material for the laser
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlitShader == null) unlitShader = Shader.Find("Unlit/Color");
            if (unlitShader == null) unlitShader = Shader.Find("Sprites/Default");

            Material laserMat = new Material(unlitShader);
            laserMat.color = laserIdleColor;
            laserMat.SetFloat("_Surface", 1f); // Transparent
            laserMat.SetFloat("_Blend", 0f);
            laserMat.renderQueue = 3000;
            laserLine.material = laserMat;

            // Gradient: vivid at origin, fades at tip
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.9f, 0f),
                    new GradientAlphaKey(0.1f, 1f)
                }
            );
            laserLine.colorGradient = gradient;
        }

        /// <summary>
        /// Updates the laser line positions and color every frame.
        /// </summary>
        private void UpdateLaser()
        {
            if (laserLine == null || !showLaser)
            {
                if (laserLine != null) laserLine.enabled = false;
                return;
            }

            laserLine.enabled = true;

            Vector3 origin = transform.position;
            Vector3 direction = transform.forward;
            Vector3 endPoint = origin + direction * laserLength;

            laserLine.SetPosition(0, origin);
            laserLine.SetPosition(1, endPoint);

            // Subtle breathing glow pulse
            glowPulsePhase += Time.deltaTime * 3f;
            float pulse = 0.85f + 0.15f * Mathf.Sin(glowPulsePhase);

            Color currentColor = isHoveringKey ? laserActiveColor : laserIdleColor;
            currentColor.a *= pulse;

            if (laserLine.material != null)
            {
                laserLine.material.color = currentColor;
            }

            // Adjust width when hovering: slightly wider to signal "ready to press"
            float targetWidth = isHoveringKey ? laserWidth * 1.6f : laserWidth;
            laserLine.startWidth = Mathf.Lerp(laserLine.startWidth, targetWidth, Time.deltaTime * 12f);
            laserLine.endWidth = laserLine.startWidth * 0.3f;
        }

        /// <summary>
        /// Notifies this pointer tip whether it's currently hovering over a key.
        /// Used by VirtualKeyboardController to change laser color.
        /// </summary>
        public void SetHoveringKey(bool hovering)
        {
            isHoveringKey = hovering;
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
