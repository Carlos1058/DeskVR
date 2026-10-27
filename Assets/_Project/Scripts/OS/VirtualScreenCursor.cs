using System.Collections.Generic;
using DeskVR.Peripherals;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DeskVR.OS
{
    /// <summary>
    /// Translates 3D physical mouse movement into 2D screen cursor movement.
    /// Simulates pointer events (Hover, Click, Drag) on the World Space Canvas.
    /// </summary>
    public class VirtualScreenCursor : MonoBehaviour
    {
        [Header("Screen Configuration")]
        [SerializeField] private RectTransform screenRect;
        [SerializeField] private RectTransform cursorRect;
        [SerializeField] private GraphicRaycaster raycaster;
        [SerializeField] private EventSystem eventSystem;

        [Header("Cursor Settings")]
        [SerializeField] private Vector2 initialPosition = Vector2.zero;
        [SerializeField] private float cursorSpeedMultiplier = 1.0f;

        private PointerEventData pointerData;
        private GameObject currentHoveredObject;
        private GameObject currentPressedObject;

        private void Awake()
        {
            if (cursorRect == null) cursorRect = GetComponent<RectTransform>();
            if (screenRect == null && transform.parent != null) screenRect = transform.parent.GetComponent<RectTransform>();
            if (eventSystem == null) eventSystem = EventSystem.current;
            if (raycaster == null && screenRect != null) raycaster = screenRect.GetComponentInParent<GraphicRaycaster>();
        }

        private void OnEnable()
        {
            VirtualMouseController.OnMouseMoved += HandleMouseMoved;
            VirtualMouseController.OnLeftClick += HandleLeftClick;
        }

        private void OnDisable()
        {
            VirtualMouseController.OnMouseMoved -= HandleMouseMoved;
            VirtualMouseController.OnLeftClick -= HandleLeftClick;
        }

        private void Start()
        {
            if (cursorRect != null)
            {
                cursorRect.anchoredPosition = initialPosition;
            }
            pointerData = new PointerEventData(eventSystem);
        }

        private void HandleMouseMoved(Vector2 delta)
        {
            if (cursorRect == null || screenRect == null) return;

            Vector2 pos = cursorRect.anchoredPosition;
            pos += delta * cursorSpeedMultiplier;

            // Clamp cursor inside screen bounds
            float halfW = screenRect.rect.width * 0.5f;
            float halfH = screenRect.rect.height * 0.5f;

            pos.x = Mathf.Clamp(pos.x, -halfW, halfW);
            pos.y = Mathf.Clamp(pos.y, -halfH, halfH);

            cursorRect.anchoredPosition = pos;

            UpdateHover();
        }

        private void UpdateHover()
        {
            if (raycaster == null || eventSystem == null) return;

            // Convert world position of cursor to screen raycast
            pointerData.position = RectTransformUtility.WorldToScreenPoint(Camera.main, cursorRect.position);
            var results = new List<RaycastResult>();
            raycaster.Raycast(pointerData, results);

            GameObject newHovered = results.Count > 0 ? results[0].gameObject : null;

            if (newHovered != currentHoveredObject)
            {
                if (currentHoveredObject != null)
                {
                    ExecuteEvents.Execute(currentHoveredObject, pointerData, ExecuteEvents.pointerExitHandler);
                }
                currentHoveredObject = newHovered;
                if (currentHoveredObject != null)
                {
                    ExecuteEvents.Execute(currentHoveredObject, pointerData, ExecuteEvents.pointerEnterHandler);
                }
            }
        }

        private void HandleLeftClick()
        {
            if (raycaster == null || eventSystem == null) return;

            pointerData.position = RectTransformUtility.WorldToScreenPoint(Camera.main, cursorRect.position);
            var results = new List<RaycastResult>();
            raycaster.Raycast(pointerData, results);

            if (results.Count > 0)
            {
                GameObject target = results[0].gameObject;

                // Simulate PointerDown and Click
                ExecuteEvents.Execute(target, pointerData, ExecuteEvents.pointerDownHandler);
                ExecuteEvents.Execute(target, pointerData, ExecuteEvents.pointerClickHandler);
                ExecuteEvents.Execute(target, pointerData, ExecuteEvents.pointerUpHandler);
            }
        }
    }
}
