using UnityEngine;
using UnityEditor;

public class DebugPrefabStructure
{
    [MenuItem("Tools/Debug ObjectPrefabHolder Structure")]
    public static void DebugStructure()
    {
        string templatePath = "Assets/Prefabs/ObjectPrefabHolder.prefab";
        GameObject template = AssetDatabase.LoadAssetAtPath<GameObject>(templatePath);

        if (template == null)
        {
            Debug.LogError($"ObjectPrefabHolder not found at {templatePath}");
            return;
        }

        Debug.Log("=== ObjectPrefabHolder Structure ===");
        PrintHierarchy(template.transform, 0);
    }

    static void PrintHierarchy(Transform t, int indent)
    {
        string indentStr = new string(' ', indent * 2);
        Debug.Log($"{indentStr}└─ {t.name}");

        foreach (Transform child in t)
        {
            PrintHierarchy(child, indent + 1);
        }
    }
}
