using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ClockworkGrid
{
    /// <summary>
    /// Debug menu for testing game mechanics.
    /// Allows toggling debug placement mode and adjusting token count.
    /// </summary>
    public class DebugMenu : MonoBehaviour
    {
        private GameObject debugPanel;
        private Button toggleButton;
        private TextMeshProUGUI toggleButtonText;
        private TextMeshProUGUI instructionsText;
        private bool isDebugMode = false;

        public bool IsDebugModeActive => isDebugMode;

        public void Initialize(Canvas canvas)
        {
            CreateDebugPanel(canvas);
            UpdateUI();
        }

        private void CreateDebugPanel(Canvas canvas)
        {
            // Main panel container (top-right, below token display)
            debugPanel = new GameObject("DebugPanel");
            debugPanel.transform.SetParent(canvas.transform, false);

            RectTransform panelRect = debugPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(1f, 1f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.pivot = new Vector2(1f, 1f);
            panelRect.anchoredPosition = new Vector2(-20f, -80f); // Below token display
            panelRect.sizeDelta = new Vector2(280f, 180f);

            // Background
            Image panelBg = debugPanel.AddComponent<Image>();
            panelBg.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);

            // Title
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(debugPanel.transform, false);
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -10f);
            titleRect.sizeDelta = new Vector2(-20f, 30f);

            TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "DEBUG MENU";
            titleText.fontSize = 18;
            titleText.color = Color.yellow;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.fontStyle = FontStyles.Bold;

            // Toggle Debug Mode Button
            CreateToggleButton();

            // Token adjustment controls
            CreateTokenControls();

            // Instructions
            CreateInstructions();
        }

        private void CreateToggleButton()
        {
            GameObject buttonObj = new GameObject("ToggleButton");
            buttonObj.transform.SetParent(debugPanel.transform, false);

            RectTransform buttonRect = buttonObj.AddComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.5f, 1f);
            buttonRect.anchorMax = new Vector2(0.5f, 1f);
            buttonRect.pivot = new Vector2(0.5f, 1f);
            buttonRect.anchoredPosition = new Vector2(0f, -45f);
            buttonRect.sizeDelta = new Vector2(220f, 35f);

            Image buttonBg = buttonObj.AddComponent<Image>();
            buttonBg.color = new Color(0.3f, 0.3f, 0.3f, 1f);

            toggleButton = buttonObj.AddComponent<Button>();
            toggleButton.targetGraphic = buttonBg;
            toggleButton.onClick.AddListener(ToggleDebugMode);

            // Button text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            toggleButtonText = textObj.AddComponent<TextMeshProUGUI>();
            toggleButtonText.text = "Debug Mode: OFF";
            toggleButtonText.fontSize = 16;
            toggleButtonText.color = Color.white;
            toggleButtonText.alignment = TextAlignmentOptions.Center;
            toggleButtonText.fontStyle = FontStyles.Bold;
        }

        private void CreateTokenControls()
        {
            GameObject controlsObj = new GameObject("TokenControls");
            controlsObj.transform.SetParent(debugPanel.transform, false);

            RectTransform controlsRect = controlsObj.AddComponent<RectTransform>();
            controlsRect.anchorMin = new Vector2(0.5f, 1f);
            controlsRect.anchorMax = new Vector2(0.5f, 1f);
            controlsRect.pivot = new Vector2(0.5f, 1f);
            controlsRect.anchoredPosition = new Vector2(0f, -90f);
            controlsRect.sizeDelta = new Vector2(260f, 30f);

            // Label
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(controlsObj.transform, false);
            RectTransform labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0.5f);
            labelRect.anchorMax = new Vector2(0f, 0.5f);
            labelRect.pivot = new Vector2(0f, 0.5f);
            labelRect.anchoredPosition = new Vector2(0f, 0f);
            labelRect.sizeDelta = new Vector2(80f, 30f);

            TextMeshProUGUI labelText = labelObj.AddComponent<TextMeshProUGUI>();
            labelText.text = "Tokens:";
            labelText.fontSize = 14;
            labelText.color = Color.white;
            labelText.alignment = TextAlignmentOptions.MidlineLeft;

            // Buttons: -10, -1, +1, +10, +100
            float[] amounts = { -10f, -1f, 1f, 10f, 100f };
            string[] labels = { "-10", "-1", "+1", "+10", "+100" };
            float startX = 85f;
            float buttonWidth = 35f;
            float spacing = 35f;

            for (int i = 0; i < amounts.Length; i++)
            {
                float amount = amounts[i];
                CreateTokenButton(controlsObj.transform, labels[i], amount, startX + i * spacing, buttonWidth);
            }
        }

        private void CreateTokenButton(Transform parent, string label, float amount, float xPos, float width)
        {
            GameObject btnObj = new GameObject($"TokenBtn{label}");
            btnObj.transform.SetParent(parent, false);

            RectTransform btnRect = btnObj.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0f, 0.5f);
            btnRect.anchorMax = new Vector2(0f, 0.5f);
            btnRect.pivot = new Vector2(0f, 0.5f);
            btnRect.anchoredPosition = new Vector2(xPos, 0f);
            btnRect.sizeDelta = new Vector2(width, 25f);

            Image btnBg = btnObj.AddComponent<Image>();
            btnBg.color = amount > 0 ? new Color(0.2f, 0.6f, 0.2f, 1f) : new Color(0.6f, 0.2f, 0.2f, 1f);

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = btnBg;
            btn.onClick.AddListener(() => AdjustTokens((int)amount));

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 12;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.fontStyle = FontStyles.Bold;
        }

        private void CreateInstructions()
        {
            GameObject instrObj = new GameObject("Instructions");
            instrObj.transform.SetParent(debugPanel.transform, false);

            RectTransform instrRect = instrObj.AddComponent<RectTransform>();
            instrRect.anchorMin = new Vector2(0f, 0f);
            instrRect.anchorMax = new Vector2(1f, 0f);
            instrRect.pivot = new Vector2(0.5f, 0f);
            instrRect.anchoredPosition = new Vector2(0f, 10f);
            instrRect.sizeDelta = new Vector2(-20f, 50f);

            instructionsText = instrObj.AddComponent<TextMeshProUGUI>();
            instructionsText.text = "";
            instructionsText.fontSize = 11;
            instructionsText.color = new Color(1f, 1f, 0.5f);
            instructionsText.alignment = TextAlignmentOptions.TopLeft;
        }

        private void ToggleDebugMode()
        {
            isDebugMode = !isDebugMode;
            UpdateUI();

            // Notify CellDebugPlacer
            CellDebugPlacer placer = FindObjectOfType<CellDebugPlacer>();
            if (placer != null)
            {
                placer.SetDebugMode(isDebugMode);
            }

            Debug.Log($"Debug Mode: {(isDebugMode ? "ENABLED" : "DISABLED")}");
        }

        private void AdjustTokens(int amount)
        {
            if (ResourceTokenManager.Instance == null) return;

            int currentTokens = ResourceTokenManager.Instance.CurrentTokens;
            int newTotal = Mathf.Max(0, currentTokens + amount);
            int actualChange = newTotal - currentTokens;

            if (actualChange > 0)
            {
                ResourceTokenManager.Instance.AddTokens(actualChange);
            }
            else if (actualChange < 0)
            {
                ResourceTokenManager.Instance.SpendTokens(-actualChange);
            }

            Debug.Log($"Tokens adjusted by {amount}. New total: {newTotal}");
        }

        private void UpdateUI()
        {
            if (isDebugMode)
            {
                toggleButtonText.text = "Debug Mode: ON";
                toggleButtonText.color = Color.green;
                instructionsText.text = "Right-click: Place Resource\nMiddle-click: Place Enemy";
            }
            else
            {
                toggleButtonText.text = "Debug Mode: OFF";
                toggleButtonText.color = Color.white;
                instructionsText.text = "";
            }
        }
    }
}
