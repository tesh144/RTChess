using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LittleCafe;

namespace ClockworkGrid
{
    /// <summary>
    /// Simple UI for saving and loading cafe layouts.
    /// Shows save/load buttons and a text field for codes.
    /// </summary>
    public class SaveLoadUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Button saveButton;
        [SerializeField] private Button loadButton;
        [SerializeField] private TMP_InputField codeInputField;
        [SerializeField] private TextMeshProUGUI feedbackText;

        [Header("Feedback Settings")]
        [SerializeField] private float feedbackDisplayTime = 3f;

        private float feedbackTimer = 0f;

        private void Start()
        {
            if (saveButton != null)
                saveButton.onClick.AddListener(OnSaveButtonClicked);

            if (loadButton != null)
                loadButton.onClick.AddListener(OnLoadButtonClicked);

            if (feedbackText != null)
                feedbackText.gameObject.SetActive(false);
        }

        private void Update()
        {
            // Hide feedback text after timer expires
            if (feedbackTimer > 0f)
            {
                feedbackTimer -= Time.deltaTime;
                if (feedbackTimer <= 0f && feedbackText != null)
                {
                    feedbackText.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// Save current layout to clipboard as code.
        /// </summary>
        private void OnSaveButtonClicked()
        {
            string json = LayoutSerializer.SerializeLayout();
            if (string.IsNullOrEmpty(json))
            {
                ShowFeedback("Failed to save layout!", Color.red);
                return;
            }

            string code = LayoutSerializer.CompressToCode(json);
            LayoutSerializer.CopyToClipboard(code);

            if (codeInputField != null)
            {
                codeInputField.text = code;
            }

            ShowFeedback("Layout saved to clipboard!", Color.green);
            Debug.Log($"[SaveLoadUI] Layout code copied to clipboard (length: {code.Length})");
        }

        /// <summary>
        /// Load layout from code in input field (or clipboard).
        /// </summary>
        private void OnLoadButtonClicked()
        {
            string code = null;

            // Try to get code from input field first
            if (codeInputField != null && !string.IsNullOrEmpty(codeInputField.text))
            {
                code = codeInputField.text.Trim();
            }
            // Otherwise, try clipboard
            else
            {
                code = LayoutSerializer.GetFromClipboard();
            }

            if (string.IsNullOrEmpty(code))
            {
                ShowFeedback("No code found! Paste code or use clipboard.", Color.red);
                return;
            }

            // Load layout
            LayoutLoader loader = LayoutLoader.Instance;
            if (loader == null)
            {
                ShowFeedback("LayoutLoader not found in scene!", Color.red);
                Debug.LogError("[SaveLoadUI] LayoutLoader.Instance is null!");
                return;
            }

            bool success = loader.LoadLayout(code, clearExisting: true);

            if (success)
            {
                ShowFeedback("Layout loaded successfully!", Color.green);
            }
            else
            {
                ShowFeedback("Failed to load layout!", Color.red);
            }
        }

        /// <summary>
        /// Show feedback message for a few seconds.
        /// </summary>
        private void ShowFeedback(string message, Color color)
        {
            if (feedbackText == null) return;

            feedbackText.text = message;
            feedbackText.color = color;
            feedbackText.gameObject.SetActive(true);
            feedbackTimer = feedbackDisplayTime;
        }
    }
}
