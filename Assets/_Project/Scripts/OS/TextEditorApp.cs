using System;
using System.Collections;
using DeskVR.Peripherals;
using UnityEngine;
using UnityEngine.UI;

namespace DeskVR.OS
{
    /// <summary>
    /// Simulated Notepad application.
    /// Receives keypresses from the 3D physical keyboard and renders text on the screen.
    /// </summary>
    public class TextEditorApp : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Text contentText;
        [SerializeField] private Text placeholderText;
        [SerializeField] private VirtualWindow parentWindow;

        [Header("Settings")]
        [SerializeField] private string initialContent = "";
        [SerializeField] private int maxCharacters = 500;

        public static event Action<string> OnContentChanged;

        private string currentText = "";
        private bool isBlinkingCursor = true;
        private Coroutine cursorCoroutine;

        public string CurrentText => currentText;

        private void Awake()
        {
            if (parentWindow == null) parentWindow = GetComponentInParent<VirtualWindow>();
        }

        private void OnEnable()
        {
            VirtualKeyboardController.OnKeyTyped += HandleKeyTyped;
            cursorCoroutine = StartCoroutine(CursorBlinkRoutine());
            UpdateDisplay();
        }

        private void OnDisable()
        {
            VirtualKeyboardController.OnKeyTyped -= HandleKeyTyped;
            if (cursorCoroutine != null)
            {
                StopCoroutine(cursorCoroutine);
                cursorCoroutine = null;
            }
        }

        private void Start()
        {
            currentText = initialContent;
            UpdateDisplay();
        }

        private void HandleKeyTyped(string charVal, KeyActionType actionType)
        {
            // Only type if the window is open and active
            if (parentWindow != null && !parentWindow.gameObject.activeInHierarchy)
            {
                return;
            }

            switch (actionType)
            {
                case KeyActionType.Character:
                    if (currentText.Length < maxCharacters)
                    {
                        currentText += charVal;
                    }
                    break;

                case KeyActionType.Space:
                    if (currentText.Length < maxCharacters)
                    {
                        currentText += " ";
                    }
                    break;

                case KeyActionType.Backspace:
                    if (currentText.Length > 0)
                    {
                        currentText = currentText.Substring(0, currentText.Length - 1);
                    }
                    break;

                case KeyActionType.Enter:
                    if (currentText.Length < maxCharacters)
                    {
                        currentText += "\n";
                    }
                    break;
            }

            OnContentChanged?.Invoke(currentText);
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            if (contentText == null) return;

            if (string.IsNullOrEmpty(currentText))
            {
                if (placeholderText != null) placeholderText.gameObject.SetActive(true);
                contentText.text = isBlinkingCursor ? "|" : "";
            }
            else
            {
                if (placeholderText != null) placeholderText.gameObject.SetActive(false);
                contentText.text = currentText + (isBlinkingCursor ? "|" : "");
            }
        }

        private IEnumerator CursorBlinkRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(0.5f);
                isBlinkingCursor = !isBlinkingCursor;
                UpdateDisplay();
            }
        }

        public void Clear()
        {
            currentText = "";
            UpdateDisplay();
        }

        public void SetText(string text)
        {
            currentText = text;
            UpdateDisplay();
        }
    }
}
