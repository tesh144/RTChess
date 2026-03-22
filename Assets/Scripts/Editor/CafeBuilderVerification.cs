#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using UnityEditor;
using System.Linq;
using LittleCafe;
using ClockworkGrid;

/// <summary>
/// Verification tool to check that Cafe Builder setup completed successfully.
/// </summary>
public class CafeBuilderVerification
{
    [MenuItem("Tools/LittleCafe/Verify Setup")]
    public static void VerifySetup()
    {
        Debug.Log("========================================");
        Debug.Log("CAFE BUILDER SETUP VERIFICATION");
        Debug.Log("========================================\n");

        bool allGood = true;

        // Check 1: FurnitureDatabase exists
        allGood &= CheckDatabase();

        // Check 2: Animations exist
        allGood &= CheckAnimations();

        // Check 3: Prefabs generated
        allGood &= CheckPrefabs();

        // Check 4: Database references
        allGood &= CheckDatabaseReferences();

        Debug.Log("\n========================================");
        if (allGood)
        {
            Debug.Log("✅ ALL CHECKS PASSED!");
            Debug.Log("You're ready to test in Play mode!");

            EditorUtility.DisplayDialog(
                "Verification Complete",
                "✅ All checks passed!\n\n" +
                "Everything is set up correctly.\n" +
                "Check the Console for detailed results.\n\n" +
                "Next: Test in Play mode!",
                "OK");
        }
        else
        {
            Debug.LogWarning("⚠️ SOME CHECKS FAILED - See details above");

            EditorUtility.DisplayDialog(
                "Verification Issues",
                "⚠️ Some checks failed.\n\n" +
                "Check the Console for details.\n" +
                "You may need to re-run setup steps.",
                "OK");
        }
        Debug.Log("========================================");
    }

    private static bool CheckDatabase()
    {
        Debug.Log("📁 Checking FurnitureDatabase...");

        FurnitureDatabase db = AssetDatabase.LoadAssetAtPath<FurnitureDatabase>(
            "Assets/Scripts/Data/FurnitureDatabase.asset");

        if (db == null)
        {
            Debug.LogError("  ❌ FurnitureDatabase not found at Assets/Data/");
            Debug.LogError("     → Run: Tools → LittleCafe → Auto-Setup Cafe Builder");
            return false;
        }

        Debug.Log($"  ✓ Database exists with {db.Count} entries");

        if (db.Count == 0)
        {
            Debug.LogError("  ❌ Database is empty!");
            Debug.LogError("     → Run: Tools → LittleCafe → Auto-Setup Cafe Builder");
            return false;
        }

        // Check type distribution
        int tables = db.GetByType(FurnitureType.Table).Count;
        int chairs = db.GetByType(FurnitureType.Chair).Count;
        int walls = db.GetByType(FurnitureType.Wall).Count;
        int decorations = db.GetByType(FurnitureType.Decoration).Count;

        Debug.Log($"  ✓ Type breakdown:");
        Debug.Log($"    - Tables: {tables}");
        Debug.Log($"    - Chairs: {chairs}");
        Debug.Log($"    - Walls: {walls}");
        Debug.Log($"    - Decorations: {decorations}");

        return true;
    }

    private static bool CheckAnimations()
    {
        Debug.Log("\n🎬 Checking Animations...");

        string[] requiredAnims = new string[]
        {
            "Assets/Animations/ObjectAnimations/Object_Appear.anim",
            "Assets/Animations/ObjectAnimations/Object_Destroy.anim",
            "Assets/Animations/ObjectAnimations/Object_Interact.anim",
            "Assets/Animations/ObjectAnimations/Object_Idle.anim"
        };

        bool allExist = true;
        int found = 0;

        foreach (string path in requiredAnims)
        {
            var anim = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (anim != null)
            {
                found++;
            }
            else
            {
                Debug.LogWarning($"  ⚠️ Missing: {System.IO.Path.GetFileName(path)}");
                allExist = false;
            }
        }

        if (allExist)
        {
            Debug.Log($"  ✓ All {requiredAnims.Length} animation files found");
        }
        else
        {
            Debug.LogWarning($"  ⚠️ Only found {found}/{requiredAnims.Length} animations");
            Debug.LogWarning("     → Run: Tools → Create Object Animations");
        }

        // Check controller
        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/Animations/ObjectAnimController.controller");

        if (controller != null)
        {
            Debug.Log("  ✓ ObjectAnimController found");
        }
        else
        {
            Debug.LogWarning("  ⚠️ ObjectAnimController not found");
            Debug.LogWarning("     → Will be created during prefab generation");
        }

        return allExist;
    }

    private static bool CheckPrefabs()
    {
        Debug.Log("\n🎁 Checking Prefabs...");

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/PEPO" });

        if (prefabGuids.Length == 0)
        {
            Debug.LogError("  ❌ No prefabs found in Assets/Prefabs/PEPO/");
            Debug.LogError("     → Run: Tools → LittleCafe → Generate PEPO Prefabs");
            return false;
        }

        Debug.Log($"  ✓ Found {prefabGuids.Length} prefabs");

        // Check a sample prefab structure
        string samplePath = AssetDatabase.GUIDToAssetPath(prefabGuids[0]);
        GameObject samplePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(samplePath);

        if (samplePrefab != null)
        {
            // Check hierarchy
            Transform animHolder = samplePrefab.transform.Find("AnimatorHolder");
            bool hasAnimHolder = animHolder != null;
            bool hasAnimator = hasAnimHolder && animHolder.GetComponent<Animator>() != null;
            bool hasGridObject = samplePrefab.GetComponent<GridObject>() != null;
            bool hasFurnitureObject = samplePrefab.GetComponent<FurnitureObject>() != null;

            Debug.Log($"  ✓ Sample prefab structure ({samplePrefab.name}):");
            Debug.Log($"    - AnimatorHolder: {(hasAnimHolder ? "✓" : "❌")}");
            Debug.Log($"    - Animator component: {(hasAnimator ? "✓" : "❌")}");
            Debug.Log($"    - GridObject component: {(hasGridObject ? "✓" : "❌")}");
            Debug.Log($"    - FurnitureObject component: {(hasFurnitureObject ? "✓" : "❌")}");

            if (!hasAnimHolder || !hasAnimator || !hasGridObject || !hasFurnitureObject)
            {
                Debug.LogWarning("  ⚠️ Sample prefab missing components");
                Debug.LogWarning("     → Re-run: Tools → LittleCafe → Generate PEPO Prefabs");
                return false;
            }
        }

        return true;
    }

    private static bool CheckDatabaseReferences()
    {
        Debug.Log("\n🔗 Checking Database References...");

        FurnitureDatabase db = AssetDatabase.LoadAssetAtPath<FurnitureDatabase>(
            "Assets/Scripts/Data/FurnitureDatabase.asset");

        if (db == null) return false;

        int withPrefabs = db.AllFurniture.Count(f => f.prefab != null);
        int withoutPrefabs = db.Count - withPrefabs;

        Debug.Log($"  Database entries with prefabs: {withPrefabs}/{db.Count}");

        if (withPrefabs == 0)
        {
            Debug.LogError("  ❌ No database entries have prefab references!");
            Debug.LogError("     → Run: Tools → LittleCafe → Generate PEPO Prefabs");
            return false;
        }

        if (withoutPrefabs > 0)
        {
            Debug.LogWarning($"  ⚠️ {withoutPrefabs} entries missing prefab references");
            Debug.LogWarning("     → Re-run prefab generation if needed");
            return false;
        }

        Debug.Log("  ✓ All database entries have prefab references");
        return true;
    }
}
