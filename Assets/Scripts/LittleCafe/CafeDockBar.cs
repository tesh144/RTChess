using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace LittleCafe
{
    /// <summary>
    /// Dock bar for equipment cards in the cafe builder.
    /// Works like RTChess's DockBarManager but for equipment placement.
    /// Cards are always available (no draw cost) — just drag to place.
    /// Assign references in the Inspector.
    /// </summary>
    public class CafeDockBar : MonoBehaviour
    {
        public static CafeDockBar Instance { get; private set; }

        [Header("Card Data (assign in Inspector)")]
        [Tooltip("Equipment cards available in the dock. Order determines display order.")]
        [SerializeField] private List<EquipmentCardData> availableCards = new List<EquipmentCardData>();

        [Header("UI References (assign in Inspector)")]
        [SerializeField] private Transform cardContainer;
        [SerializeField] private GameObject cardIconPrefab;

        [Header("Action Buttons (assign in Inspector)")]
        [SerializeField] private Button saveButton;
        [SerializeField] private Button loadButton;
        [SerializeField] private Button clearButton;
        [SerializeField] private Button startServiceButton;

        private List<CafeEquipmentIcon> cardIcons = new List<CafeEquipmentIcon>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            PopulateCards();
            WireActionButtons();
        }

        private void PopulateCards()
        {
            // Clear existing icons
            foreach (var icon in cardIcons)
            {
                if (icon != null) Destroy(icon.gameObject);
            }
            cardIcons.Clear();

            if (cardContainer == null || cardIconPrefab == null) return;

            foreach (EquipmentCardData cardData in availableCards)
            {
                if (cardData == null) continue;

                GameObject iconObj = Instantiate(cardIconPrefab, cardContainer);
                CafeEquipmentIcon icon = iconObj.GetComponent<CafeEquipmentIcon>();
                if (icon == null) icon = iconObj.AddComponent<CafeEquipmentIcon>();

                icon.Initialize(cardData);
                cardIcons.Add(icon);
            }
        }

        private void WireActionButtons()
        {
            if (saveButton != null)
                saveButton.onClick.AddListener(() => LayoutManager.Instance?.SaveToFile());
            if (loadButton != null)
                loadButton.onClick.AddListener(() => LayoutManager.Instance?.LoadFromFile());
            if (clearButton != null)
                clearButton.onClick.AddListener(() => LayoutManager.Instance?.ClearAll());
            if (startServiceButton != null)
                startServiceButton.onClick.AddListener(() => GameModeManager.Instance?.OnStartServiceClicked());
        }

        public void RefreshCards()
        {
            PopulateCards();
        }
    }
}
