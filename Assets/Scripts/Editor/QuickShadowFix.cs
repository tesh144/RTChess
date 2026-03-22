#pragma warning disable CS0414, CS0219, CS0618
using UnityEditor;
using UnityEngine;

/// <summary>
/// Quick fix: Convert shadow materials from Unlit/Color to Unlit/Transparent with proper opacity.
/// </summary>
public class QuickShadowFix : EditorWindow
{
    [MenuItem("Tools/PEPO/Quick Fix Shadow Transparency")]
    public static void FixShadowTransparency()
    {
        GameObject[] allObjects = FindObjectsOfType<GameObject>();
        int fixedCount = 0;

        foreach (GameObject obj in allObjects)
        {
            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                foreach (Material mat in renderer.sharedMaterials)
                {
                    if (mat != null && (mat.name.ToLower().Contains("shadow") || renderer.gameObject.name.ToLower().Contains("shadow")))
                    {
                        // Convert to Unlit/Transparent which DOES support alpha
                        Shader transparentShader = Shader.Find("Unlit/Transparent");
                        if (transparentShader != null)
                        {
                            mat.shader = transparentShader;
                            mat.color = new Color(0f, 0f, 0f, 0.5f); // 50% opaque black
                            fixedCount++;
                            Debug.Log($"[QuickShadowFix] Fixed shadow material: {mat.name}");
                        }
                    }
                }
            }
        }

        Debug.Log($"[QuickShadowFix] Fixed {fixedCount} shadow materials in scene");
        EditorUtility.DisplayDialog("Shadow Transparency Fixed", $"Fixed {fixedCount} shadow materials", "OK");
    }
}
