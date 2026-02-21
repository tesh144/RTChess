---
name: unity-editor-scripting
description: Use this skill for Unity Editor automation, custom tools, batch operations, menu items, inspector customization, and Editor windows. Trigger when the user wants to automate Unity workflows, create editor tools, batch process assets, add menu items (Tools/Assets menus), customize inspectors, create custom editor windows, or perform any task requiring UnityEditor namespace APIs. Use this skill when the user mentions "editor script", "batch operation", "custom tool", "menu item", "[MenuItem]", "EditorWindow", "Editor automation", or wants to automate repetitive Unity Editor tasks.
---

# Unity Editor Scripting

Automate Unity workflows, create custom tools, and batch process assets without repetitive manual work.

## When to Use This Skill

- Creating menu items (Tools → Custom Action)
- Batch processing multiple assets
- Custom Inspector/Property Drawers
- Editor Windows and tools
- Automation scripts (InitializeOnLoad)
- Asset post-processors

**Not for:** Asset creation itself (use unity-asset-management) or gameplay code (use unity-gameplay-dev).

---

## Core Patterns

### Pattern 1: Menu Item for Quick Actions

```csharp
using UnityEngine;
using UnityEditor;

public class QuickTools
{
    [MenuItem("Tools/My Tool/Do Something")]
    public static void DoSomething()
    {
        Debug.Log("Tool executed!");
        // Your logic here
    }

    // Validation (grays out menu if invalid)
    [MenuItem("Tools/My Tool/Do Something", true)]
    public static bool ValidateDoSomething()
    {
        return Selection.activeGameObject != null;
    }

    // Keyboard shortcut
    [MenuItem("Tools/Quick Action %#q")] // Ctrl+Shift+Q
    public static void QuickAction()
    {
        // Fast access action
    }
}
```

###Pattern 2: Batch Asset Processing

```csharp
using UnityEditor;
using UnityEngine;

public class BatchProcessor
{
    [MenuItem("Tools/Batch/Process All Prefabs")]
    public static void ProcessAllPrefabs()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/MyFolder" });
        int processed = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (ProcessPrefab(prefab))
            {
                EditorUtility.SetDirty(prefab);
                processed++;
            }

            // Progress bar
            EditorUtility.DisplayProgressBar(
                "Processing",
                $"Prefab {processed}/{guids.Length}",
                (float)processed / guids.Length
            );
        }

        EditorUtility.ClearProgressBar();
        AssetDatabase.SaveAssets();
        Debug.Log($"✓ Processed {processed} prefabs");
    }

    static bool ProcessPrefab(GameObject prefab)
    {
        // Your processing logic
        return true;
    }
}
```

### Pattern 3: Auto-Execute on Compilation

```csharp
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public class AutoSetup
{
    static AutoSetup()
    {
        EditorApplication.delayCall += () =>
        {
            // Runs after editor loads/recompiles
            CheckProjectSetup();
        };
    }

    static void CheckProjectSetup()
    {
        if (!System.IO.Directory.Exists("Assets/RequiredFolder"))
        {
            AssetDatabase.CreateFolder("Assets", "RequiredFolder");
            Debug.Log("✓ Created required folder");
        }
    }
}
```

### Pattern 4: Custom Editor Window

```csharp
using UnityEditor;
using UnityEngine;

public class MyToolWindow : EditorWindow
{
    private string inputText = "";

    [MenuItem("Window/My Tool")]
    public static void ShowWindow()
    {
        GetWindow<MyToolWindow>("My Tool");
    }

    void OnGUI()
    {
        GUILayout.Label("Settings", EditorStyles.boldLabel);

        inputText = EditorGUILayout.TextField("Input:", inputText);

        if (GUILayout.Button("Execute"))
        {
            Execute();
        }
    }

    void Execute()
    {
        Debug.Log($"Executed with: {inputText}");
    }
}
```

---

## Key APIs

### AssetDatabase
```csharp
AssetDatabase.FindAssets("t:Prefab")          // Find all prefabs
AssetDatabase.GUIDToAssetPath(guid)           // Convert GUID to path
AssetDatabase.LoadAssetAtPath<T>(path)        // Load asset
AssetDatabase.CreateFolder(parent, name)      // Create folder
AssetDatabase.SaveAssets()                    // Save changes
AssetDatabase.Refresh()                       // Reload database
```

### EditorUtility
```csharp
EditorUtility.DisplayProgressBar(title, info, progress)
EditorUtility.ClearProgressBar()
EditorUtility.DisplayDialog(title, message, ok, cancel)
EditorUtility.SetDirty(obj)                   // Mark for save
```

### PrefabUtility
```csharp
PrefabUtility.LoadPrefabContents(path)        // Load for editing
PrefabUtility.SaveAsPrefabAsset(obj, path)    // Save changes
PrefabUtility.UnloadPrefabContents(obj)       // Cleanup
```

---

## Best Practices

1. **Always use try/finally for LoadPrefabContents**
   ```csharp
   var prefab = PrefabUtility.LoadPrefabContents(path);
   try {
       // Modify
   } finally {
       PrefabUtility.UnloadPrefabContents(prefab);
   }
   ```

2. **Show progress for long operations**
   ```csharp
   EditorUtility.DisplayProgressBar("Processing", "Item X/Y", progress);
   EditorUtility.ClearProgressBar(); // Always clear!
   ```

3. **Validate before execution**
   ```csharp
   [MenuItem("Tools/Action", true)]
   public static bool Validate() {
       return /* check if can run */;
   }
   ```

4. **Save assets after modifications**
   ```csharp
   AssetDatabase.SaveAssets();
   AssetDatabase.Refresh();
   ```

---

## Common Tasks

### Find All Assets of Type
```csharp
string[] guids = AssetDatabase.FindAssets("t:Material");
foreach (string guid in guids) {
    string path = AssetDatabase.GUIDToAssetPath(guid);
    Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
}
```

### Context Menu on Asset
```csharp
[MenuItem("Assets/My Action")]
public static void MyAction() {
    Object selected = Selection.activeObject;
    string path = AssetDatabase.GetAssetPath(selected);
}
```

### Create Asset Programmatically
```csharp
Material mat = new Material(Shader.Find("Standard"));
AssetDatabase.CreateAsset(mat, "Assets/NewMaterial.mat");
```

---

Use this skill to build powerful Unity Editor tools that save hours of manual work!