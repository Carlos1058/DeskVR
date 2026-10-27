using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DeskVR.OS
{
    /// <summary>
    /// Represents an interactive desktop window in the simulated OS.
    /// Supports opening, closing, minimizing, focus order, and dragging via the title bar.
    /// </summary>
    public class VirtualWindow : MonoBehaviour, IPointerDownHandler
    {
        [Header("Window Identification")]
        [SerializeField] private string windowTitle = "Ventana";
        [SerializeField] private RectTransform windowRect;
        [SerializeField] private RectTransform titleBarRect;
        [SerializeField] private Text titleText;

        [Header("Buttons")]
        [SerializeField] private Button closeButton;
        [SerializeField] private Button minimizeButton;

        public static event Action<string> OnWindowOpened;
        public static event Action<string> OnWindowClosed;

        private RectTransform screenBounds;
        private bool isOpen = false;

        public bool IsOpen => isOpen;
        public string WindowTitle => windowTitle;

        private void Awake()
        {
            if (windowRect == null) windowRect = GetComponent<RectTransform>();
            if (transform.parent != null) screenBounds = transform.parent.GetComponent<RectTransform>();

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(Close);
            }
            if (minimizeButton != null)
            {
                minimizeButton.onClick.AddListener(Minimize);
            }

            // Setup Title Bar Dragging
            if (titleBarRect != null)
            {
                var dragger = titleBarRect.gameObject.GetComponent<WindowTitleBarDrag>();
                if (dragger == null) dragger = titleBarRect.gameObject.AddComponent<WindowTitleBarDrag>();
                dragger.Initialize(this);
            }

            if (titleText != null)
            {
                titleText.text = windowTitle;
            }
        }

        public void Open()
        {
            gameObject.SetActive(true);
            isOpen = true;
            transform.SetAsLastSibling(); // Bring to front
            OnWindowOpened?.Invoke(windowTitle);
        }

        public void Close()
        {
            isOpen = false;
            gameObject.SetActive(false);
            OnWindowClosed?.Invoke(windowTitle);
        }

        public void Minimize()
        {
            isOpen = false;
            gameObject.SetActive(false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            // Focus window when clicked
            transform.SetAsLastSibling();
        }

        public void DragWindow(Vector2 delta)
        {
            if (windowRect == null) return;

            Vector2 newPos = windowRect.anchoredPosition + delta;

            if (screenBounds != null)
            {
                float halfScreenW = screenBounds.rect.width * 0.5f;
                float halfScreenH = screenBounds.rect.height * 0.5f;
                float halfWinW = windowRect.rect.width * 0.5f;
                float halfWinH = windowRect.rect.height * 0.5f;

                newPos.x = Mathf.Clamp(newPos.x, -halfScreenW + halfWinW, halfScreenW - halfWinW);
                newPos.y = Mathf.Clamp(newPos.y, -halfScreenH + halfWinH, halfScreenH - halfWinH);
            }

            windowRect.anchoredPosition = newPos;
        }

        private void OnDestroy()
        {
            if (closeButton != null) closeButton.onClick.RemoveListener(Close);
            if (minimizeButton != null) minimizeButton.onClick.RemoveListener(Minimize);
        }
    }

    /// <summary>
    /// Attached to the title bar of a VirtualWindow to forward drag events.
    /// </summary>
    public class WindowTitleBarDrag : MonoBehaviour, IDragHandler, IBeginDragHandler
    {
        private VirtualWindow parentWindow;

        public void Initialize(VirtualWindow window)
        {
            parentWindow = window;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (parentWindow != null)
            {
                parentWindow.transform.SetAsLastSibling();
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (parentWindow != null)
            {
                parentWindow.DragWindow(eventData.delta);
            }
        }
    }
}
