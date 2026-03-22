#pragma warning disable CS0414, CS0219, CS0618
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using ClockworkGrid;
using LittleCafe;

/// <summary>
/// Editor tool that wires the Card_Prefab with GameCardUI component references.
///
/// The prefab was created manually from the Lobby panel button layout.
/// Expected children:
///   - Icon (Image)
///   - Text (TextMeshProUGUI)
///   - Notify_Count_Green (green badge with child Text)
///   - Notify_Count_Red (red badge with child Text)
///   - Icon_Lock (lock overlay image)
///
/// This tool:
///   1. Loads the Card_Prefab from Assets/Prefabs/UI/
///   2. Adds/finds GameCardUI component
///   3. Wires all serialized references
///   4. Sets default badge/lock states (hidden)
///   5. Saves the prefab
///
/// Menu: Tools > ClockworkCraft > Wire Card Prefab
/// </summary>
public class SetupCardPrefab
{
    [MenuItem("Tools/ClockworkCraft/Wire Card Prefab")]
    public static void Run()
    {
        // ── Find the Card_Prefab asset ───────────────────────────────

        string[] guids = AssetDatabase.FindAssets("Card_Prefab t:Prefab");
        if (guids.Length == 0)
        {
            // Also try "CardPrefab" and "GameCard"
            guids = AssetDatabase.FindAssets("CardPrefab t:Prefab");
        }
        if (guids.Length == 0)
        {
            guids = AssetDatabase.FindAssets("GameCard t:Prefab");
        }

        if (guids.Length == 0)
        {
            Debug.LogError("[SetupCardPrefab] No Card_Prefab found! " +
                "Create a prefab named 'Card_Prefab' in Assets/Prefabs/UI/");
            return;
        }

        string prefabPath = AssetDatabase.GUIDToAssetPath(guids[0]);
        Debug.Log($"[SetupCardPrefab] Found prefab at: {prefabPath}");

        // ── Load prefab for editing ──────────────────────────────────

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        if (prefabRoot == null)
        {
            Debug.LogError("[SetupCardPrefab] Failed to load prefab contents!");
            return;
        }

        try
        {
            // ── Find child elements ──────────────────────────────────

            // Icon
            Transform iconTransform = prefabRoot.transform.Find("Icon");
            Image iconImage = iconTransform != null ? iconTransform.GetComponent<Image>() : null;

            // Text (name label)
            Transform textTransform = prefabRoot.transform.Find("Text");
            TextMeshProUGUI nameText = textTransform != null
                ? textTransform.GetComponent<TextMeshProUGUI>()
                : null;

            // Green badge (Notify_Count_Green)
            Transform greenTransform = prefabRoot.transform.Find("Notify_Count_Green");
            GameObject greenBadge = greenTransform != null ? greenTransform.gameObject : null;
            TextMeshProUGUI greenBadgeText = greenTransform != null
                ? greenTransform.GetComponentInChildren<TextMeshProUGUI>()
                : null;

            // Red badge (Notify_Count_Red)
            Transform redTransform = prefabRoot.transform.Find("Notify_Count_Red");
            GameObject redBadge = redTransform != null ? redTransform.gameObject : null;

            // Lock overlay (Icon_Lock)
            Transform lockTransform = prefabRoot.transform.Find("Icon_Lock");
            GameObject lockOverlay = lockTransform != null ? lockTransform.gameObject : null;

            // Button component on root
            Button cardButton = prefabRoot.GetComponent<Button>();

            // Background image on root
            Image cardBackground = prefabRoot.GetComponent<Image>();

            // ── Add or find GameCardUI ───────────────────────────────

            GameCardUI cardUI = prefabRoot.GetComponent<GameCardUI>();
            if (cardUI == null)
                cardUI = prefabRoot.AddComponent<GameCardUI>();

            // ── Assign references via SerializedObject ───────────────

            var so = new SerializedObject(cardUI);

            SetRef(so, "iconImage", iconImage);
            SetRef(so, "nameText", nameText);
            SetRef(so, "greenBadge", greenBadge);
            SetRef(so, "greenBadgeText", greenBadgeText);
            SetRef(so, "redBadge", redBadge);
            SetRef(so, "lockOverlay", lockOverlay);
            SetRef(so, "cardButton", cardButton);
            SetRef(so, "cardBackground", cardBackground);

            so.ApplyModifiedProperties();

            // ── Set default states ───────────────────────────────────

            // Badges hidden by default
            if (greenBadge != null) greenBadge.SetActive(false);
            if (redBadge != null) redBadge.SetActive(false);

            // Lock overlay hidden by default; reposition to center if off-screen
            if (lockOverlay != null)
            {
                lockOverlay.SetActive(false);
                RectTransform lockRT = lockOverlay.GetComponent<RectTransform>();
                if (lockRT != null)
                {
                    // Center the lock icon on the card
                    lockRT.anchorMin = new Vector2(0.5f, 0.5f);
                    lockRT.anchorMax = new Vector2(0.5f, 0.5f);
                    lockRT.anchoredPosition = Vector2.zero;
                }
            }

            // ── Save prefab ──────────────────────────────────────────

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);

            Debug.Log($"[SetupCardPrefab] Card_Prefab wired successfully!\n" +
                      $"  Icon: {(iconImage != null ? "OK" : "MISSING")}\n" +
                      $"  Name Text: {(nameText != null ? "OK" : "MISSING")}\n" +
                      $"  Green Badge: {(greenBadge != null ? "OK" : "not found")}\n" +
                      $"  Green Badge Text: {(greenBadgeText != null ? "OK" : "not found")}\n" +
                      $"  Red Badge: {(redBadge != null ? "OK" : "not found")}\n" +
                      $"  Lock Overlay: {(lockOverlay != null ? "OK" : "not found")}\n" +
                      $"  Button: {(cardButton != null ? "OK" : "MISSING")}\n" +
                      $"  Background: {(cardBackground != null ? "OK" : "MISSING")}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        // Ping the prefab in the Project window
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (asset != null)
        {
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }
    }

    private static void SetRef(SerializedObject so, string propName, Object value)
    {
        var prop = so.FindProperty(propName);
        if (prop != null && value != null)
            prop.objectReferenceValue = value;
    }
}
#endif
