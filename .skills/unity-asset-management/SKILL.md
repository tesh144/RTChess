---
name: unity-asset-management
description: Use this skill for all Unity prefab, material, FBX import, shader, and texture work. Trigger whenever the user mentions prefabs, materials, shaders, textures, FBX files, asset organization, or any Unity asset pipeline tasks. This includes creating/modifying prefabs, converting materials between shaders, importing FBX files, setting up textures, organizing project assets, or any task involving Unity's Asset Database. Always use this skill when working with .prefab, .mat, .fbx, .png/.jpg texture files in Unity, or when the user asks to "convert materials", "setup prefabs", "import FBX", or mentions Unity asset-related work.
---

# Unity Asset Management

Master prefabs, materials, FBX imports, and Unity's asset system without the common failures.

## Core Principle: Two Layers

**All Unity asset failures stem from confusing these two layers:**

```
ASSET LAYER (Disk - Persistent)     RUNTIME LAYER (Memory - Temporary)
├─ .mat files                        ├─ Material instances
├─ .prefab files                     ├─ GameObject instances
├─ AssetDatabase API                 ├─ GetComponent<>()
├─ PrefabUtility API                 ├─ material.SetColor()
└─ Saved permanently                 └─ Lost on reload
```

**Golden Rule:** Use Asset Layer APIs for permanent changes. Runtime Layer APIs are for gameplay only.

---

## MANDATORY: Ask Before Proceeding

When the user requests asset work, **ALWAYS ask these clarifying questions FIRST:**

### Materials & Shaders:
1. **"Should I create new .mat asset files or modify existing materials?"**
   - New .mat files = separate, reusable
   - Modify existing = changes affect all users of that material

2. **"Do you want this material shared across objects or per-instance?"**
   - Shared = one .mat file, all objects sync
   - Per-instance = each object can differ

3. **"Should textures be preserved when converting shaders?"**
   - Yes = need to remap properties
   - No = use default shader settings

### Prefabs:
1. **"Should I modify the prefab asset or create a prefab variant?"**
   - Modify = changes affect all instances
   - Variant = inherits from original, can override

2. **"Do existing scene instances need to update automatically?"**
   - Yes = use PrefabUtility patterns
   - No = can work on prefab in isolation

### FBX Import:
1. **"Does this FBX have baked shadows/materials I should preserve?"**
   - Check before generating new geometry
   - FBX materials might be intentional

2. **"What should happen to FBX materials on import?"**
   - Keep Standard shader?
   - Convert to Unlit?
   - Extract and customize?

---

## Pattern 1: Material Conversion (Asset Layer)

**Problem:** Material shader changes don't persist.

**Why:** `material.shader = newShader` modifies memory, not disk.

**Solution:**
```csharp
using UnityEditor;
using UnityEngine;

// Load prefab safely
GameObject prefabContents = PrefabUtility.LoadPrefabContents(prefabPath);

try
{
    MeshRenderer[] renderers = prefabContents.GetComponentsInChildren<MeshRenderer>();

    foreach (MeshRenderer renderer in renderers)
    {
        Material[] materials = renderer.sharedMaterials;

        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i] != null)
            {
                // Save current properties
                Texture mainTex = materials[i].mainTexture;
                Color col = materials[i].HasProperty("_Color") ? materials[i].color : Color.white;

                // Change shader (properties preserved automatically if names match)
                materials[i].shader = Shader.Find("Unlit/Texture");

                // Restore if needed
                if (mainTex != null)
                {
                    materials[i].mainTexture = mainTex;
                }
                materials[i].color = col;
            }
        }

        renderer.sharedMaterials = materials;
    }

    // Save changes to prefab asset
    PrefabUtility.SaveAsPrefabAsset(prefabContents, prefabPath);
}
finally
{
    // CRITICAL: Always unload
    PrefabUtility.UnloadPrefabContents(prefabContents);
}

// Persist to disk
AssetDatabase.SaveAssets();
AssetDatabase.Refresh();
```

**Key Points:**
- `LoadPrefabContents` = isolated editing environment
- `sharedMaterials` = modifies the actual material references
- `SaveAsPrefabAsset` = writes changes to disk
- `UnloadPrefabContents` in finally = prevents asset locking
- `AssetDatabase.SaveAssets()` = commits to disk

---

## Pattern 2: Create Persistent Material Assets

**Problem:** `new Material()` creates temporary material that's lost on reload.

**Solution:**
```csharp
using UnityEditor;
using UnityEngine;

public static Material CreatePersistentMaterial(string name, Shader shader, Texture mainTexture = null)
{
    // Ensure directory exists
    string dirPath = "Assets/Materials";
    if (!AssetDatabase.IsValidFolder(dirPath))
    {
        AssetDatabase.CreateFolder("Assets", "Materials");
    }

    // Create material
    Material mat = new Material(shader);
    mat.name = name;

    if (mainTexture != null)
    {
        mat.mainTexture = mainTexture;
    }

    // Save as .mat asset file
    string assetPath = $"{dirPath}/{name}.mat";
    AssetDatabase.CreateAsset(mat, assetPath);
    AssetDatabase.SaveAssets();

    return mat;
}
```

**When to use:**
- Creating new materials from scratch
- Need material to persist across sessions
- Want to reference material from multiple prefabs

---

## Pattern 3: Check Shader Properties Before Setting

**Problem:** Silent failures when shader doesn't have expected property.

**Solution:**
```csharp
void SetMaterialPropertySafely(Material mat, string propertyName, float value)
{
    if (mat.HasProperty(propertyName))
    {
        mat.SetFloat(propertyName, value);
    }
    else
    {
        Debug.LogWarning($"Material '{mat.name}' shader '{mat.shader.name}' doesn't have property '{propertyName}'");
    }
}

// Common properties by shader:
// Standard: _Color, _MainTex, _Metallic, _Glossiness, _BumpMap
// Unlit/Texture: _Color, _MainTex
// Unlit/Color: _Color only
// Unlit/Transparent: _Color, _MainTex
```

---

## Pattern 4: FBX Shadow Meshes (Don't Generate, Use Existing)

**Problem:** Trying to "generate" shadow quads when FBX already has shadow geometry.

**Solution:**
```csharp
using UnityEditor;
using UnityEngine;

// Find FBX shadow meshes (by material name pattern)
public static void ConvertFBXShadowsToAnimatable(GameObject prefabRoot, Material shadowMaterial)
{
    MeshRenderer[] renderers = prefabRoot.GetComponentsInChildren<MeshRenderer>(true);

    foreach (MeshRenderer renderer in renderers)
    {
        // Check if this is a shadow mesh
        bool isShadowMesh = false;
        foreach (Material mat in renderer.sharedMaterials)
        {
            if (mat != null && mat.name.ToLower().Contains("shadow"))
            {
                isShadowMesh = true;
                break;
            }
        }

        if (isShadowMesh)
        {
            // Rename for animation targeting
            renderer.gameObject.name = "Shadow";

            // Assign animatable shadow material
            Material[] mats = new Material[renderer.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++)
            {
                mats[i] = shadowMaterial;
            }
            renderer.sharedMaterials = mats;

            break; // Only process first shadow mesh
        }
    }
}
```

**Key insight:** FBX files often have pre-modeled shadow geometry. Don't create new quads—use what's there.

---

## Common Failure Patterns & Solutions

### 1. "Material changes don't persist"
**Symptom:** Changes work in Editor, lost after reload/build.

**Diagnosis:**
```csharp
// WRONG: Runtime modification (lost on reload)
renderer.material.color = Color.blue;

// RIGHT: Asset modification (persists)
Material mat = renderer.sharedMaterial;
mat.color = Color.blue;
EditorUtility.SetDirty(mat);
AssetDatabase.SaveAssets();
```

### 2. "Prefab modifications don't save"
**Symptom:** Script modifies prefab, changes don't appear.

**Diagnosis:**
```csharp
// WRONG: Modifying instance doesn't change prefab
var instance = Instantiate(prefab);
instance.GetComponent<Renderer>().material = newMat;

// RIGHT: Load, modify, save prefab asset
var prefabContents = PrefabUtility.LoadPrefabContents(prefabPath);
prefabContents.GetComponent<Renderer>().material = newMat;
PrefabUtility.SaveAsPrefabAsset(prefabContents, prefabPath);
PrefabUtility.UnloadPrefabContents(prefabContents);
```

### 3. "Shader conversion breaks textures"
**Symptom:** Convert shader, textures disappear.

**Diagnosis:**
```csharp
// WRONG: Just change shader
mat.shader = Shader.Find("Unlit/Texture");

// RIGHT: Preserve textures when changing
Texture tex = mat.mainTexture;
mat.shader = Shader.Find("Unlit/Texture");
mat.mainTexture = tex; // Restore if property names differ
```

---

## Validation Checklist

After any asset modification, verify:

```csharp
public static bool ValidatePrefabChanges(string prefabPath)
{
    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
    if (prefab == null)
    {
        Debug.LogError($"Prefab not found: {prefabPath}");
        return false;
    }

    // Check materials exist and have valid shaders
    MeshRenderer[] renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
    foreach (var renderer in renderers)
    {
        foreach (var mat in renderer.sharedMaterials)
        {
            if (mat == null)
            {
                Debug.LogError($"Null material in {prefab.name}");
                return false;
            }

            if (mat.shader == null)
            {
                Debug.LogError($"Null shader in material {mat.name}");
                return false;
            }

            // Check for magenta (missing shader)
            if (mat.shader.name == "Hidden/InternalErrorShader")
            {
                Debug.LogError($"Missing shader in {mat.name}");
                return false;
            }
        }
    }

    Debug.Log($"✓ Validation passed: {prefab.name}");
    return true;
}
```

---

## API Reference Quick Lookup

### Asset Layer (Permanent)
```csharp
AssetDatabase.CreateAsset(obj, path)      // Create new asset file
AssetDatabase.SaveAssets()                // Commit changes to disk
AssetDatabase.Refresh()                   // Reload asset database
PrefabUtility.LoadPrefabContents(path)    // Load for editing
PrefabUtility.SaveAsPrefabAsset(obj, path)// Save changes
PrefabUtility.UnloadPrefabContents(obj)   // Release lock
EditorUtility.SetDirty(obj)               // Mark for saving
```

### Runtime Layer (Temporary)
```csharp
renderer.material                         // Per-instance copy
renderer.sharedMaterial                   // Shared reference
Instantiate(prefab)                       // Create copy
GetComponent<Renderer>()                  // Access runtime component
```

---

## When NOT to Use This Skill

This skill is for **asset pipeline work** (prefabs, materials, FBX). Don't use for:

- **Gameplay code**: Use unity-gameplay-dev skill
- **Editor tools/windows**: Use unity-editor-scripting skill
- **Scene setup**: This is runtime work, not asset work
- **Build configuration**: Different domain

---

## Summary: What This Skill Prevents

✅ Material changes that don't persist
✅ Prefab modifications that disappear
✅ Shader property errors (silent failures)
✅ FBX import confusion
✅ Texture loss during shader conversion
✅ Mixing up Asset Layer vs Runtime Layer APIs
✅ Forgetting to save/unload prefab contents
✅ Creating temporary materials instead of assets

**Use this skill for all Unity asset work and avoid the failures that wasted hours today.**
