using System;
using System.Collections;
using DeskVR.Peripherals;
using UnityEngine;
using UnityEngine.UI;

namespace DeskVR.OS
{
    public enum PracticeState
    {
        Step1_WriteHola,
        Step2_BackspaceCorrection,
        Completed
    }

    /// <summary>
    /// Implements the pedagogical instruction and accessible feedback module from DIX09 and DIX10.
    /// Provides clear step-by-step goals, audio-independent visual feedback, smart key highlighting,
    /// and safe error tolerance for users with low digital literacy or situational limitations.
    /// </summary>
    public class DeskVRPedagogicalGuide : MonoBehaviour
    {
        [Header("System References")]
        [SerializeField] private TextEditorApp textEditor;
        [SerializeField] private VirtualKeyboardController keyboardController;

        [Header("UI Panels & Text")]
        [SerializeField] private Text stepBadgeText;
        [SerializeField] private Text mainInstructionText;
        [SerializeField] private Text subInstructionText;
        [SerializeField] private Text feedbackBannerText;
        [SerializeField] private Image feedbackBannerBg;

        [Header("Letter Progress Slots")]
        [SerializeField] private Text[] letterSlotTexts; // H, O, L, A
        [SerializeField] private Image[] letterSlotBgs;

        [Header("Action Buttons")]
        [SerializeField] private Button restartBtn;
        [SerializeField] private Button hintBtn;

        [Header("Settings")]
        [SerializeField] private float smartHintDelay = 3.5f;

        // Visual Colors for High Accessibility & Contrast
        private readonly Color colorStepActive = new Color(0.12f, 0.45f, 0.95f);
        private readonly Color colorStepCompleted = new Color(0.16f, 0.68f, 0.32f);
        private readonly Color colorBannerNeutral = new Color(0.16f, 0.20f, 0.28f, 0.92f);
        private readonly Color colorBannerSuccess = new Color(0.12f, 0.55f, 0.28f, 0.95f);
        private readonly Color colorBannerNotice = new Color(0.85f, 0.55f, 0.12f, 0.95f);

        private readonly Color colorSlotPending = new Color(0.88f, 0.90f, 0.94f);
        private readonly Color colorSlotActive = new Color(1.0f, 0.92f, 0.65f);
        private readonly Color colorSlotDone = new Color(0.70f, 0.92f, 0.75f);
        private readonly Color colorSlotErased = new Color(0.95f, 0.85f, 0.85f);

        private PracticeState currentState = PracticeState.Step1_WriteHola;
        private const string TargetWord = "HOLA";
        private int currentLetterIndex = 0;
        private Coroutine hintCoroutine;
        private Coroutine bannerResetCoroutine;

        public PracticeState CurrentState => currentState;

        private void OnEnable()
        {
            VirtualKeyboardController.OnKeyTyped += HandleKeyTyped;
            if (restartBtn != null) restartBtn.onClick.AddListener(ResetPractice);
            if (hintBtn != null) hintBtn.onClick.AddListener(TriggerManualHint);
        }

        private void OnDisable()
        {
            VirtualKeyboardController.OnKeyTyped -= HandleKeyTyped;
            if (restartBtn != null) restartBtn.onClick.RemoveListener(ResetPractice);
            if (hintBtn != null) hintBtn.onClick.RemoveListener(TriggerManualHint);
            StopAllCoroutines();
        }

        private void Start()
        {
            if (keyboardController == null)
            {
                keyboardController = VirtualKeyboardController.Instance ?? FindFirstObjectByType<VirtualKeyboardController>();
            }

            if (textEditor == null)
            {
                textEditor = FindFirstObjectByType<TextEditorApp>();
            }

            ResetPractice();
        }

        public void ResetPractice()
        {
            currentState = PracticeState.Step1_WriteHola;
            currentLetterIndex = 0;

            if (textEditor != null)
            {
                textEditor.Clear();
            }

            if (keyboardController != null)
            {
                keyboardController.ClearAllHighlights();
            }

            UpdateStep1Visuals();
            SetFeedbackBanner("💡 Usa la punta de tu control para tocar las teclas en la mesa.", colorBannerNeutral);
            RestartSmartHintTimer();
        }

        private void HandleKeyTyped(string charVal, KeyActionType actionType)
        {
            switch (currentState)
            {
                case PracticeState.Step1_WriteHola:
                    HandleStep1Key(charVal, actionType);
                    break;

                case PracticeState.Step2_BackspaceCorrection:
                    HandleStep2Key(actionType);
                    break;
            }
        }

        private void HandleStep1Key(string charVal, KeyActionType actionType)
        {
            if (actionType == KeyActionType.Character && !string.IsNullOrEmpty(charVal))
            {
                char typed = char.ToUpperInvariant(charVal[0]);
                char expected = TargetWord[currentLetterIndex];

                // Audio-independent visual feedback for key press (DIX10)
                SetFeedbackBanner($"Tecla pulsada: [{typed}]", colorBannerNeutral, 1.8f);

                if (typed == expected)
                {
                    // Correct letter!
                    if (keyboardController != null)
                    {
                        keyboardController.HighlightKey(expected.ToString(), false);
                    }

                    currentLetterIndex++;
                    UpdateStep1Visuals();

                    if (currentLetterIndex >= TargetWord.Length)
                    {
                        // HOLA completed!
                        StartCoroutine(TransitionToStep2Routine());
                    }
                    else
                    {
                        RestartSmartHintTimer();
                    }
                }
                else
                {
                    // Erroneous letter: non-punitive guidance
                    SetFeedbackBanner($"Pulsaste '{typed}'. La letra esperada es '{expected}'.", colorBannerNotice, 3f);
                    RestartSmartHintTimer(1.0f); // Fast hint to assist user
                }
            }
            else if (actionType == KeyActionType.Backspace)
            {
                // If user hit backspace in step 1, gracefully adapt
                if (currentLetterIndex > 0)
                {
                    currentLetterIndex--;
                    UpdateStep1Visuals();
                    RestartSmartHintTimer();
                }
            }
        }

        private void HandleStep2Key(KeyActionType actionType)
        {
            if (actionType == KeyActionType.Backspace)
            {
                // Target action reached!
                currentState = PracticeState.Completed;

                if (keyboardController != null)
                {
                    keyboardController.ClearAllHighlights();
                }

                // DIX09 & DIX10 primary confirmation text:
                SetFeedbackBanner("✅ Correcto, borraste una letra.", colorBannerSuccess, 0f);

                UpdateCompletedVisuals();
            }
            else
            {
                SetFeedbackBanner("Presiona la tecla [Borrar ⌫] para quitar la letra 'A'.", colorBannerNotice, 2.5f);
            }
        }

        private void UpdateStep1Visuals()
        {
            if (stepBadgeText != null)
            {
                stepBadgeText.text = "PASO 1 DE 2 · ESCRITURA";
                stepBadgeText.color = colorStepActive;
            }

            if (mainInstructionText != null)
            {
                mainInstructionText.text = "Escribe \"HOLA\" en el Bloc de Notas";
            }

            if (subInstructionText != null)
            {
                if (currentLetterIndex < TargetWord.Length)
                {
                    char nextChar = TargetWord[currentLetterIndex];
                    subInstructionText.text = $"Siguiente letra a pulsar: <color=#0078D7><b>[{nextChar}]</b></color>";
                }
                else
                {
                    subInstructionText.text = "¡Excelente! Palabra completa.";
                }
            }

            // Update letter progress slots
            for (int i = 0; i < 4; i++)
            {
                if (letterSlotTexts != null && i < letterSlotTexts.Length && letterSlotTexts[i] != null)
                {
                    letterSlotTexts[i].text = TargetWord[i].ToString();
                    letterSlotTexts[i].color = i < currentLetterIndex ? new Color(0.08f, 0.45f, 0.18f) : new Color(0.25f, 0.28f, 0.35f);
                }

                if (letterSlotBgs != null && i < letterSlotBgs.Length && letterSlotBgs[i] != null)
                {
                    if (i < currentLetterIndex)
                        letterSlotBgs[i].color = colorSlotDone;
                    else if (i == currentLetterIndex)
                        letterSlotBgs[i].color = colorSlotActive;
                    else
                        letterSlotBgs[i].color = colorSlotPending;
                }
            }
        }

        private IEnumerator TransitionToStep2Routine()
        {
            SetFeedbackBanner("¡Muy bien! Escribiste HOLA.", colorBannerSuccess, 2.0f);
            yield return new WaitForSeconds(1.2f);

            currentState = PracticeState.Step2_BackspaceCorrection;

            if (stepBadgeText != null)
            {
                stepBadgeText.text = "PASO 2 DE 2 · CORRECCIÓN";
                stepBadgeText.color = new Color(0.85f, 0.40f, 0.10f);
            }

            if (mainInstructionText != null)
            {
                mainInstructionText.text = "Borra la última letra (\"A\")";
            }

            if (subInstructionText != null)
            {
                subInstructionText.text = "Presiona la tecla <color=#D83B01><b>[Borrar ⌫]</b></color> en el teclado 3D.";
            }

            // Highlight Backspace key in gold on virtual keyboard
            if (keyboardController != null)
            {
                keyboardController.ClearAllHighlights();
                keyboardController.HighlightKey("Borrar ⌫", true);
                keyboardController.HighlightKey("Borrar", true);
            }

            // Mark slot 4 as target for deletion
            if (letterSlotBgs != null && letterSlotBgs.Length > 3 && letterSlotBgs[3] != null)
            {
                letterSlotBgs[3].color = colorSlotActive;
            }

            SetFeedbackBanner("Identifica la tecla [Borrar ⌫] (iluminada en dorado) y tócala.", colorBannerNotice);
        }

        private void UpdateCompletedVisuals()
        {
            if (stepBadgeText != null)
            {
                stepBadgeText.text = "COMPLETADO";
                stepBadgeText.color = colorStepCompleted;
            }

            if (mainInstructionText != null)
            {
                mainInstructionText.text = "🎉 ¡Práctica completada con éxito!";
            }

            if (subInstructionText != null)
            {
                subInstructionText.text = "Aprendiste a escribir texto y a corregir letras con la tecla de borrado.";
            }

            // Update slot 4 to show erased status
            if (letterSlotBgs != null && letterSlotBgs.Length > 3 && letterSlotBgs[3] != null)
            {
                letterSlotBgs[3].color = colorSlotErased;
            }
            if (letterSlotTexts != null && letterSlotTexts.Length > 3 && letterSlotTexts[3] != null)
            {
                letterSlotTexts[3].text = "—";
            }
        }

        private void RestartSmartHintTimer(float delayOverride = -1f)
        {
            if (hintCoroutine != null) StopCoroutine(hintCoroutine);
            hintCoroutine = StartCoroutine(SmartHintRoutine(delayOverride > 0f ? delayOverride : smartHintDelay));
        }

        private IEnumerator SmartHintRoutine(float delay)
        {
            yield return new WaitForSeconds(delay);

            if (currentState == PracticeState.Step1_WriteHola && currentLetterIndex < TargetWord.Length)
            {
                string expectedStr = TargetWord[currentLetterIndex].ToString();
                if (keyboardController != null)
                {
                    keyboardController.HighlightKey(expectedStr, true);
                }
                SetFeedbackBanner($"Pista: Toca la tecla [{expectedStr}] iluminada en dorado.", colorBannerNotice, 4f);
            }
            else if (currentState == PracticeState.Step2_BackspaceCorrection)
            {
                if (keyboardController != null)
                {
                    keyboardController.HighlightKey("Borrar ⌫", true);
                    keyboardController.HighlightKey("Borrar", true);
                }
            }
        }

        public void TriggerManualHint()
        {
            if (currentState == PracticeState.Step1_WriteHola && currentLetterIndex < TargetWord.Length)
            {
                string expectedStr = TargetWord[currentLetterIndex].ToString();
                if (keyboardController != null)
                {
                    keyboardController.HighlightKey(expectedStr, true);
                }
                SetFeedbackBanner($"Pista activada: Busca la tecla [{expectedStr}] en dorado.", colorBannerNotice, 4f);
            }
            else if (currentState == PracticeState.Step2_BackspaceCorrection)
            {
                if (keyboardController != null)
                {
                    keyboardController.HighlightKey("Borrar ⌫", true);
                }
                SetFeedbackBanner("Pista activada: La tecla [Borrar ⌫] está iluminada en dorado.", colorBannerNotice, 4f);
            }
        }

        public void SetFeedbackBanner(string message, Color bgColor, float autoDismissSeconds = 0f)
        {
            if (feedbackBannerText != null)
            {
                feedbackBannerText.text = message;
            }

            if (feedbackBannerBg != null)
            {
                feedbackBannerBg.color = bgColor;
            }

            if (bannerResetCoroutine != null) StopCoroutine(bannerResetCoroutine);

            if (autoDismissSeconds > 0f)
            {
                bannerResetCoroutine = StartCoroutine(BannerAutoDismissRoutine(autoDismissSeconds));
            }
        }

        private IEnumerator BannerAutoDismissRoutine(float seconds)
        {
            yield return new WaitForSeconds(seconds);

            if (currentState == PracticeState.Step1_WriteHola)
            {
                SetFeedbackBanner("💡 Usa la punta de tu control para tocar las teclas en la mesa.", colorBannerNeutral);
            }
            else if (currentState == PracticeState.Step2_BackspaceCorrection)
            {
                SetFeedbackBanner("Pulsa la tecla [Borrar ⌫] en el teclado 3D.", colorBannerNotice);
            }
        }
    }
}
