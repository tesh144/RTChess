using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using LittleCafe;
using ClockworkGrid;

/// <summary>
/// Editor wizard that creates equipment prefabs, card data assets, and sets up the LittleCafe scene.
/// Run via menu: LittleCafe > Setup Scene.
/// After running, everything is in-scene and adjustable in the Inspector.
/// </summary>
public class CafeBuilderSetupWizard : Editor
{
    private const string PrefabFolder = "Assets/LittleCafe/Prefabs/Equipment";
    private const string MaterialFolder = "Assets/LittleCafe/Materials";
    private const string CardDataFolder = "Assets/LittleCafe/Data/Cards";
    private const string UIPrefabFolder = "Assets/LittleCafe/Prefabs/UI";

    // All equipment types and their names
    private static readonly (EquipmentType type, string name)[] AllEquipment = new[]
    {
        (EquipmentType.Table, "Table"),
        (EquipmentType.Chair, "Chair"),
        (EquipmentType.Wall, "Wall"),
        (EquipmentType.Door, "Door"),
        (EquipmentType.CookingStation, "CookingStation"),
        (EquipmentType.ServingCounter, "ServingCounter"),
        (EquipmentType.WashingStation, "WashingStation"),
        (EquipmentType.PlateRack, "PlateRack"),
    };

    [MenuItem("LittleCafe/Setup Scene", false, 10)]
    public static void SetupScene()
    {
        if (!EditorUtility.DisplayDialog("LittleCafe Scene Setup",
            "This will create equipment prefabs, card data, and set up the scene hierarchy.\n\n" +
            "Existing LittleCafe objects in the scene will be replaced.\n\n" +
            "Continue?", "Setup", "Cancel"))
            return;

        CreateFolders();
        GameObject[] prefabs = CreateEquipmentPrefabs();
        EquipmentCardData[] cards = CreateCardDataAssets(prefabs);
        GameObject cardIconPrefab = CreateCardIconPrefab();
        SetupSceneHierarchy(prefabs, cards, cardIconPrefab);

        Debug.Log("[CafeBuilderSetupWizard] Scene setup complete! Check Inspector references on CafeSceneSetup.");
    }

    [MenuItem("LittleCafe/Create Equipment Prefabs Only", false, 20)]
    public static void CreatePrefabsOnly()
    {
        CreateFolders();
        CreateEquipmentPrefabs();
        Debug.Log("[CafeBuilderSetupWizard] Equipment prefabs created.");
    }

    [MenuItem("LittleCafe/Create Card Data Only", false, 30)]
    public static void CreateCardDataOnly()
    {
        CreateFolders();
        GameObject[] prefabs = CreateEquipmentPrefabs();
        CreateCardDataAssets(prefabs);
        Debug.Log("[CafeBuilderSetupWizard] Card data assets created.");
    }

    // --- Folder Creation ---

    private static void CreateFolders()
    {
        CreateFolderIfNeeded("Assets/LittleCafe");
        CreateFolderIfNeeded("Assets/LittleCafe/Prefabs");
        CreateFolderIfNeeded(PrefabFolder);
        CreateFolderIfNeeded(UIPrefabFolder);
        CreateFolderIfNeeded(MaterialFolder);
        CreateFolderIfNeeded("Assets/LittleCafe/Data");
        CreateFolderIfNeeded(CardDataFolder);
    }

    private static void CreateFolderIfNeeded(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string parent = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
            string folder = System.IO.Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, folder);
        }
    }

    // --- Equipment Prefabs ---

    private static GameObject[] CreateEquipmentPrefabs()
    {
        GameObject[] prefabs = new GameObject[AllEquipment.Length];

        for (int i = 0; i < AllEquipment.Length; i++)
        {
            prefabs[i] = CreateOrUpdateEquipmentPrefab(AllEquipment[i].type, AllEquipment[i].name);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return prefabs;
    }

    private static GameObject CreateOrUpdateEquipmentPrefab(EquipmentType type, string name)
    {
        string prefabPath = $"{PrefabFolder}/{name}.prefab";
        string matPath = $"{MaterialFolder}/{name}_Mat.mat";

        // Create or update material
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(mat, matPath);
        }
        mat.color = EquipmentData.GetColor(type);
        EditorUtility.SetDirty(mat);

        // Check if prefab already exists
        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (existingPrefab != null)
        {
            MeshRenderer rend = existingPrefab.GetComponent<MeshRenderer>();
            if (rend == null) rend = existingPrefab.GetComponentInChildren<MeshRenderer>();
            if (rend != null) rend.sharedMaterial = mat;
            EditorUtility.SetDirty(existingPrefab);
            return existingPrefab;
        }

        // Create new prefab from cube
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.localScale = new Vector3(0.8f, 0.6f, 0.8f);

        MeshRenderer renderer = cube.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = mat;

        cube.AddComponent<CafeEquipment>();

        if (cube.GetComponent<BoxCollider>() == null)
            cube.AddComponent<BoxCollider>();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(cube, prefabPath);
        DestroyImmediate(cube);

        Debug.Log($"[CafeBuilderSetupWizard] Created prefab: {prefabPath}");
        return prefab;
    }

    // --- Card Data ScriptableObjects ---

    private static EquipmentCardData[] CreateCardDataAssets(GameObject[] prefabs)
    {
        EquipmentCardData[] cards = new EquipmentCardData[AllEquipment.Length];

        for (int i = 0; i < AllEquipment.Length; i++)
        {
            cards[i] = CreateOrUpdateCardData(AllEquipment[i].type, AllEquipment[i].name, prefabs[i]);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return cards;
    }

    private static EquipmentCardData CreateOrUpdateCardData(EquipmentType type, string name, GameObject prefab)
    {
        string assetPath = $"{CardDataFolder}/{name}_Card.asset";

        EquipmentCardData card = AssetDatabase.LoadAssetAtPath<EquipmentCardData>(assetPath);
        if (card == null)
        {
            card = ScriptableObject.CreateInstance<EquipmentCardData>();
            AssetDatabase.CreateAsset(card, assetPath);
        }

        card.equipmentType = type;
        card.displayName = EquipmentData.GetDisplayName(type);
        card.cardColor = EquipmentData.GetColor(type);
        card.equipmentPrefab = prefab;
        card.unlimited = true;

        EditorUtility.SetDirty(card);
        return card;
    }

    // --- Card Icon UI Prefab ---

    private static GameObject CreateCardIconPrefab()
    {
        string prefabPath = $"{UIPrefabFolder}/EquipmentCardIcon.prefab";

        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (existingPrefab != null) return existingPrefab;

        // Create card icon prefab: a small rectangle with color swatch + name
        GameObject cardObj = new GameObject("EquipmentCardIcon");
        RectTransform cardRect = cardObj.AddComponent<RectTransform>();
        cardRect.sizeDelta = new Vector2(80f, 90f);

        Image cardBg = cardObj.AddComponent<Image>();
        cardBg.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);

        // Vertical layout
        VerticalLayoutGroup vlg = cardObj.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 4f;
        vlg.padding = new RectOffset(6, 6, 6, 6);
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        // Color swatch (represents the equipment)
        GameObject swatchObj = new GameObject("ColorSwatch");
        swatchObj.transform.SetParent(cardObj.transform, false);
        RectTransform swatchRect = swatchObj.AddComponent<RectTransform>();
        swatchRect.sizeDelta = new Vector2(0f, 50f);
        Image swatchImg = swatchObj.AddComponent<Image>();
        swatchImg.color = Color.white;

        // Name label
        GameObject labelObj = new GameObject("NameLabel");
        labelObj.transform.SetParent(cardObj.transform, false);
        RectTransform labelRect = labelObj.AddComponent<RectTransform>();
        labelRect.sizeDelta = new Vector2(0f, 20f);
        TextMeshProUGUI label = labelObj.AddComponent<TextMeshProUGUI>();
        label.text = "Name";
        label.fontSize = 10;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;

        // Add CafeEquipmentIcon component
        CafeEquipmentIcon icon = cardObj.AddComponent<CafeEquipmentIcon>();

        // Wire serialized fields
        SerializedObject iconSO = new SerializedObject(icon);
        iconSO.FindProperty("colorSwatch").objectReferenceValue = swatchImg;
        iconSO.FindProperty("nameLabel").objectReferenceValue = label;
        iconSO.ApplyModifiedProperties();

        // Save as prefab
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(cardObj, prefabPath);
        DestroyImmediate(cardObj);

        Debug.Log($"[CafeBuilderSetupWizard] Created card icon prefab: {prefabPath}");
        return prefab;
    }

    // --- Scene Hierarchy ---

    private static void SetupSceneHierarchy(GameObject[] prefabs, EquipmentCardData[] cards, GameObject cardIconPrefab)
    {
        GridManager gridManager = Object.FindObjectOfType<GridManager>(true);
        CameraController cameraController = Object.FindObjectOfType<CameraController>(true);

        // --- CafeSceneSetup ---
        CafeSceneSetup sceneSetup = Object.FindObjectOfType<CafeSceneSetup>(true);
        if (sceneSetup == null)
        {
            GameObject obj = new GameObject("CafeSceneSetup");
            sceneSetup = obj.AddComponent<CafeSceneSetup>();
        }
        SerializedObject setupSO = new SerializedObject(sceneSetup);
        if (gridManager != null)
            setupSO.FindProperty("gridManager").objectReferenceValue = gridManager;
        if (cameraController != null)
            setupSO.FindProperty("cameraController").objectReferenceValue = cameraController;
        setupSO.ApplyModifiedProperties();

        // --- Managers ---
        Transform managersParent = FindOrCreateGameObject("Managers").transform;

        GameModeManager gmm = Object.FindObjectOfType<GameModeManager>(true);
        if (gmm == null)
        {
            GameObject obj = new GameObject("GameModeManager");
            obj.transform.SetParent(managersParent);
            gmm = obj.AddComponent<GameModeManager>();
        }

        LayoutManager lm = Object.FindObjectOfType<LayoutManager>(true);
        if (lm == null)
        {
            GameObject obj = new GameObject("LayoutManager");
            obj.transform.SetParent(managersParent);
            lm = obj.AddComponent<LayoutManager>();
        }
        // Wire all equipment prefabs (order matches AllEquipment array)
        SerializedObject lmSO = new SerializedObject(lm);
        for (int i = 0; i < AllEquipment.Length; i++)
        {
            string fieldName = AllEquipment[i].type switch
            {
                EquipmentType.CookingStation => "cookingStationPrefab",
                EquipmentType.ServingCounter => "servingCounterPrefab",
                EquipmentType.WashingStation => "washingStationPrefab",
                EquipmentType.PlateRack => "plateRackPrefab",
                EquipmentType.Wall => "wallPrefab",
                EquipmentType.Door => "doorPrefab",
                EquipmentType.Table => "tablePrefab",
                EquipmentType.Chair => "chairPrefab",
                _ => null
            };
            if (fieldName != null)
            {
                var prop = lmSO.FindProperty(fieldName);
                if (prop != null) prop.objectReferenceValue = prefabs[i];
            }
        }
        lmSO.ApplyModifiedProperties();

        // --- EquipmentPlacer ---
        EquipmentPlacer placer = Object.FindObjectOfType<EquipmentPlacer>(true);
        if (placer == null)
        {
            GameObject obj = new GameObject("EquipmentPlacer");
            placer = obj.AddComponent<EquipmentPlacer>();
        }

        // --- EventSystem ---
        if (Object.FindObjectOfType<EventSystem>() == null)
        {
            GameObject obj = new GameObject("EventSystem");
            obj.AddComponent<EventSystem>();
            obj.AddComponent<StandaloneInputModule>();
        }

        // --- BuildModeCanvas with Dock Bar ---
        GameObject buildCanvas = SetupBuildModeCanvas(cards, cardIconPrefab);

        // --- PlayModeCanvas ---
        GameObject playCanvas = SetupPlayModeCanvas();

        // --- Wire GameModeManager ---
        SerializedObject gmmSO = new SerializedObject(gmm);
        gmmSO.FindProperty("buildModeUI").objectReferenceValue = buildCanvas;
        gmmSO.FindProperty("playModeUI").objectReferenceValue = playCanvas;
        gmmSO.ApplyModifiedProperties();

        // Mark scene dirty
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
    }

    // --- Build Mode UI with Dock Bar ---

    private static GameObject SetupBuildModeCanvas(EquipmentCardData[] cards, GameObject cardIconPrefab)
    {
        GameObject canvasObj = FindOrCreateGameObject("BuildModeCanvas");

        Canvas canvas = canvasObj.GetComponent<Canvas>();
        if (canvas == null) canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        if (canvasObj.GetComponent<GraphicRaycaster>() == null)
            canvasObj.AddComponent<GraphicRaycaster>();

        // --- Dock Bar (bottom of screen, like RTChess) ---
        GameObject dockObj = FindOrCreateChild(canvasObj, "DockBar");
        RectTransform dockRect = EnsureRectTransform(dockObj);
        dockRect.anchorMin = new Vector2(0.1f, 0f);
        dockRect.anchorMax = new Vector2(0.9f, 0f);
        dockRect.pivot = new Vector2(0.5f, 0f);
        dockRect.anchoredPosition = new Vector2(0f, 10f);
        dockRect.sizeDelta = new Vector2(0f, 110f);

        Image dockBg = dockObj.GetComponent<Image>();
        if (dockBg == null) dockBg = dockObj.AddComponent<Image>();
        dockBg.color = new Color(0.12f, 0.12f, 0.12f, 0.9f);

        // Card container inside dock (horizontal layout)
        GameObject cardContainerObj = FindOrCreateChild(dockObj, "CardContainer");
        RectTransform containerRect = EnsureRectTransform(cardContainerObj);
        containerRect.anchorMin = new Vector2(0f, 0f);
        containerRect.anchorMax = new Vector2(1f, 1f);
        containerRect.sizeDelta = Vector2.zero;
        containerRect.offsetMin = new Vector2(10f, 5f);
        containerRect.offsetMax = new Vector2(-10f, -5f);

        HorizontalLayoutGroup hlg = cardContainerObj.GetComponent<HorizontalLayoutGroup>();
        if (hlg == null) hlg = cardContainerObj.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8f;
        hlg.padding = new RectOffset(4, 4, 4, 4);
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        // Add CafeDockBar component
        CafeDockBar dockBar = dockObj.GetComponent<CafeDockBar>();
        if (dockBar == null) dockBar = dockObj.AddComponent<CafeDockBar>();

        // --- Action buttons (top-right) ---
        Button btnSave = CreateActionButton(canvasObj, "Btn_Save", "Save",
            new Color(0.2f, 0.5f, 0.7f), new Vector2(1f, 1f), new Vector2(-20f, -20f), new Vector2(100f, 40f));
        Button btnLoad = CreateActionButton(canvasObj, "Btn_Load", "Load",
            new Color(0.2f, 0.7f, 0.4f), new Vector2(1f, 1f), new Vector2(-130f, -20f), new Vector2(100f, 40f));
        Button btnClear = CreateActionButton(canvasObj, "Btn_Clear", "Clear",
            new Color(0.7f, 0.2f, 0.2f), new Vector2(1f, 1f), new Vector2(-240f, -20f), new Vector2(100f, 40f));
        Button btnStartService = CreateActionButton(canvasObj, "Btn_StartService", "Start Service",
            new Color(0.8f, 0.6f, 0.1f), new Vector2(1f, 1f), new Vector2(-20f, -70f), new Vector2(220f, 40f));

        // Wire CafeDockBar references
        SerializedObject dockSO = new SerializedObject(dockBar);

        // Populate availableCards array
        SerializedProperty cardsProp = dockSO.FindProperty("availableCards");
        cardsProp.arraySize = cards.Length;
        for (int i = 0; i < cards.Length; i++)
        {
            cardsProp.GetArrayElementAtIndex(i).objectReferenceValue = cards[i];
        }

        dockSO.FindProperty("cardContainer").objectReferenceValue = cardContainerObj.transform;
        dockSO.FindProperty("cardIconPrefab").objectReferenceValue = cardIconPrefab;
        dockSO.FindProperty("saveButton").objectReferenceValue = btnSave;
        dockSO.FindProperty("loadButton").objectReferenceValue = btnLoad;
        dockSO.FindProperty("clearButton").objectReferenceValue = btnClear;
        dockSO.FindProperty("startServiceButton").objectReferenceValue = btnStartService;
        dockSO.ApplyModifiedProperties();

        return canvasObj;
    }

    private static GameObject SetupPlayModeCanvas()
    {
        GameObject canvasObj = FindOrCreateGameObject("PlayModeCanvas");

        Canvas canvas = canvasObj.GetComponent<Canvas>();
        if (canvas == null) canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        if (canvasObj.GetComponent<GraphicRaycaster>() == null)
            canvasObj.AddComponent<GraphicRaycaster>();

        CreateActionButton(canvasObj, "Btn_EditKitchen", "Edit Kitchen",
            new Color(0.2f, 0.6f, 0.8f), new Vector2(1f, 0f), new Vector2(-20f, 20f), new Vector2(180f, 50f));

        canvasObj.SetActive(false);
        return canvasObj;
    }

    // --- UI Helpers ---

    private static Button CreateActionButton(GameObject parent, string name, string label, Color color,
        Vector2 anchor, Vector2 anchoredPos, Vector2 size)
    {
        GameObject btnObj = FindOrCreateChild(parent, name);
        RectTransform btnRect = EnsureRectTransform(btnObj);
        btnRect.anchorMin = anchor;
        btnRect.anchorMax = anchor;
        btnRect.pivot = anchor;
        btnRect.anchoredPosition = anchoredPos;
        btnRect.sizeDelta = size;

        Image btnImg = btnObj.GetComponent<Image>();
        if (btnImg == null) btnImg = btnObj.AddComponent<Image>();
        btnImg.color = color;

        Button btn = btnObj.GetComponent<Button>();
        if (btn == null) btn = btnObj.AddComponent<Button>();

        ColorBlock colors = btn.colors;
        colors.normalColor = color;
        colors.highlightedColor = color * 1.2f;
        colors.pressedColor = color * 0.8f;
        btn.colors = colors;

        GameObject textObj = FindOrCreateChild(btnObj, "Text");
        RectTransform textRect = EnsureRectTransform(textObj);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        TextMeshProUGUI text = textObj.GetComponent<TextMeshProUGUI>();
        if (text == null) text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 14;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.fontStyle = FontStyles.Bold;

        return btn;
    }

    // --- Utility ---

    private static GameObject FindOrCreateGameObject(string name)
    {
        GameObject obj = GameObject.Find(name);
        if (obj == null) obj = new GameObject(name);
        return obj;
    }

    private static GameObject FindOrCreateChild(GameObject parent, string name)
    {
        Transform existing = parent.transform.Find(name);
        if (existing != null) return existing.gameObject;

        GameObject child = new GameObject(name);
        child.transform.SetParent(parent.transform, false);
        return child;
    }

    private static RectTransform EnsureRectTransform(GameObject obj)
    {
        RectTransform rect = obj.GetComponent<RectTransform>();
        if (rect == null) rect = obj.AddComponent<RectTransform>();
        return rect;
    }
}
