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
    /// Generates a crisp OS arrow cursor and reliably handles VR camera raycasting.
    /// </summary>
    public class VirtualScreenCursor : MonoBehaviour
    {
        [Header("Screen Configuration")]
        [SerializeField] private RectTransform screenRect;
        [SerializeField] private RectTransform cursorRect;
        [SerializeField] private GraphicRaycaster raycaster;
        [SerializeField] private EventSystem eventSystem;
        [SerializeField] private Camera eventCamera;

        [Header("Cursor Settings")]
        [SerializeField] private Vector2 initialPosition = Vector2.zero;
        [SerializeField] private float cursorSpeedMultiplier = 1.0f;

        private PointerEventData pointerData;
        private GameObject currentHoveredObject;
        private GameObject currentPressedObject;
        private bool isDragging = false;

        private void Awake()
        {
            if (cursorRect == null) cursorRect = GetComponent<RectTransform>();
            if (screenRect == null && transform.parent != null) screenRect = transform.parent.GetComponent<RectTransform>();
            if (eventSystem == null) eventSystem = EventSystem.current;
            if (raycaster == null && screenRect != null) raycaster = screenRect.GetComponentInParent<GraphicRaycaster>();

            EnsureCursorVisual();
        }

        private void OnEnable()
        {
            VirtualMouseController.OnMouseMoved += HandleMouseMoved;
            VirtualMouseController.OnLeftClickDown += HandleLeftClickDown;
            VirtualMouseController.OnLeftClickUp += HandleLeftClickUp;
            VirtualMouseController.OnLeftClick += HandleInstantClick;
        }

        private void OnDisable()
        {
            VirtualMouseController.OnMouseMoved -= HandleMouseMoved;
            VirtualMouseController.OnLeftClickDown -= HandleLeftClickDown;
            VirtualMouseController.OnLeftClickUp -= HandleLeftClickUp;
            VirtualMouseController.OnLeftClick -= HandleInstantClick;
        }

        private void Start()
        {
            if (cursorRect != null)
            {
                cursorRect.anchoredPosition = initialPosition;
            }
            if (eventSystem == null) eventSystem = EventSystem.current;
            pointerData = new PointerEventData(eventSystem);

            EnsureCursorVisual();
            UpdateHover();
        }

        public Camera GetEventCamera()
        {
            if (eventCamera != null) return eventCamera;

            if (screenRect != null)
            {
                Canvas canvas = screenRect.GetComponentInParent<Canvas>();
                if (canvas != null && canvas.worldCamera != null)
                {
                    eventCamera = canvas.worldCamera;
                    return eventCamera;
                }
            }

            if (Camera.main != null)
            {
                eventCamera = Camera.main;
                return eventCamera;
            }

            eventCamera = FindFirstObjectByType<Camera>();
            return eventCamera;
        }

        private Vector2 GetPointerScreenPosition()
        {
            Camera cam = GetEventCamera();
            if (cam != null && cursorRect != null)
            {
                return RectTransformUtility.WorldToScreenPoint(cam, cursorRect.position);
            }
            return Vector2.zero;
        }

        private void HandleMouseMoved(Vector2 delta)
        {
            if (cursorRect == null || screenRect == null) return;

            Vector2 pos = cursorRect.anchoredPosition;
            pos += delta * cursorSpeedMultiplier;

            // Screen bounds clamping
            float halfW = screenRect.rect.width * 0.5f;
            float halfH = screenRect.rect.height * 0.5f;

            pos.x = Mathf.Clamp(pos.x, -halfW, halfW);
            pos.y = Mathf.Clamp(pos.y, -halfH, halfH);

            cursorRect.anchoredPosition = pos;

            if (pointerData == null) pointerData = new PointerEventData(eventSystem);
            pointerData.position = GetPointerScreenPosition();
            pointerData.delta = delta;

            if (currentPressedObject != null)
            {
                if (!isDragging)
                {
                    isDragging = true;
                    ExecuteEvents.Execute(currentPressedObject, pointerData, ExecuteEvents.beginDragHandler);
                }
                ExecuteEvents.Execute(currentPressedObject, pointerData, ExecuteEvents.dragHandler);
            }
            else
            {
                UpdateHover();
            }
        }

        private void UpdateHover()
        {
            if (raycaster == null)
            {
                if (screenRect != null) raycaster = screenRect.GetComponentInParent<GraphicRaycaster>();
                if (raycaster == null) return;
            }

            if (pointerData == null) pointerData = new PointerEventData(eventSystem);
            pointerData.position = GetPointerScreenPosition();

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

        private void HandleLeftClickDown()
        {
            if (raycaster == null)
            {
                if (screenRect != null) raycaster = screenRect.GetComponentInParent<GraphicRaycaster>();
                if (raycaster == null) return;
            }

            if (pointerData == null) pointerData = new PointerEventData(eventSystem);
            pointerData.position = GetPointerScreenPosition();

            var results = new List<RaycastResult>();
            raycaster.Raycast(pointerData, results);

            if (results.Count > 0)
            {
                currentPressedObject = results[0].gameObject;
                pointerData.pointerPressRaycast = results[0];
                pointerData.pressPosition = pointerData.position;
                pointerData.rawPointerPress = currentPressedObject;

                ExecuteEvents.Execute(currentPressedObject, pointerData, ExecuteEvents.pointerDownHandler);
            }
        }

        private void HandleLeftClickUp()
        {
            if (currentPressedObject != null)
            {
                if (isDragging)
                {
                    ExecuteEvents.Execute(currentPressedObject, pointerData, ExecuteEvents.endDragHandler);
                    isDragging = false;
                }

                ExecuteEvents.Execute(currentPressedObject, pointerData, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(currentPressedObject, pointerData, ExecuteEvents.pointerClickHandler);
                currentPressedObject = null;
            }
            UpdateHover();
        }

        private void HandleInstantClick()
        {
            if (currentPressedObject == null)
            {
                HandleLeftClickDown();
                HandleLeftClickUp();
            }
        }

        private void EnsureCursorVisual()
        {
            var img = cursorRect != null ? cursorRect.GetComponent<Image>() : GetComponent<Image>();
            if (img != null && (img.sprite == null || img.sprite.name != "DeskVR_ArrowCursor"))
            {
                img.sprite = CreateArrowCursorSprite();
                img.color = Color.white;
                img.raycastTarget = false;

                if (cursorRect != null)
                {
                    cursorRect.pivot = new Vector2(0.08f, 0.92f); // Hotspot at arrow tip
                    cursorRect.sizeDelta = new Vector2(36f, 48f);
                }
            }
        }

        private Sprite CreateArrowCursorSprite()
        {
            int w = 32;
            int h = 42;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.name = "DeskVR_ArrowCursor";

            Color transparent = new Color(0, 0, 0, 0);
            Color fill = Color.white;
            Color border = new Color(0.08f, 0.08f, 0.10f, 1f);

            // Clear texture
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    tex.SetPixel(x, y, transparent);
                }
            }

            // Draw a crisp classic desktop mouse pointer arrow
            // Tip is at (2, h - 3)
            int tipX = 2;
            int tipY = h - 3;

            for (int row = 0; row < 36; row++)
            {
                int py = tipY - row;
                if (py < 0) continue;

                int maxWidth = Mathf.Min(w - 2, 2 + Mathf.RoundToInt(row * 0.72f));
                if (row > 22)
                {
                    // Tail of the arrow
                    maxWidth = Mathf.Max(6, 26 - row);
                }

                for (int px = tipX; px <= tipX + maxWidth; px++)
                {
                    if (px >= w) continue;
                    // Border if at boundary
                    if (px == tipX || px == tipX + maxWidth || row == 0 || row == 35)
                    {
                        tex.SetPixel(px, py, border);
                    }
                    else
                    {
                        tex.SetPixel(px, py, fill);
                    }
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.08f, 0.92f), 100f);
        }
    }
}
