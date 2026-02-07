using UnityEngine;

namespace ClockworkGrid
{
    /// <summary>
    /// Builds a 3D resource node model from primitives at runtime.
    /// Creates a crystal/tree-like shape clearly distinct from unit models.
    /// Includes a world-space HP bar above the node.
    /// </summary>
    public static class ResourceNodeModelBuilder
    {
        public static GameObject CreateResourceNodeModel(Color nodeColor)
        {
            GameObject root = new GameObject("ResourceNodeModel");

            // Main crystal body - stretched cube rotated 45 degrees
            GameObject crystal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crystal.transform.SetParent(root.transform);
            crystal.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            crystal.transform.localScale = new Vector3(0.4f, 0.7f, 0.4f);
            crystal.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            SetColor(crystal, nodeColor);

            // Small crystal shard on top
            GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shard.transform.SetParent(root.transform);
            shard.transform.localPosition = new Vector3(0.1f, 0.85f, 0.05f);
            shard.transform.localScale = new Vector3(0.15f, 0.3f, 0.15f);
            shard.transform.localRotation = Quaternion.Euler(0f, 30f, 10f);
            SetColor(shard, LightenColor(nodeColor, 0.3f));

            // Second small shard
            GameObject shard2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shard2.transform.SetParent(root.transform);
            shard2.transform.localPosition = new Vector3(-0.15f, 0.7f, -0.1f);
            shard2.transform.localScale = new Vector3(0.12f, 0.25f, 0.12f);
            shard2.transform.localRotation = Quaternion.Euler(5f, 60f, -8f);
            SetColor(shard2, LightenColor(nodeColor, 0.15f));

            // Base - flat cylinder
            GameObject basePlate = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            basePlate.transform.SetParent(root.transform);
            basePlate.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            basePlate.transform.localScale = new Vector3(0.5f, 0.05f, 0.5f);
            SetColor(basePlate, DarkenColor(nodeColor, 0.4f));

            // Add HP text above node
            CreateHPText(root);

            // Remove colliders from visual parts
            RemoveCollider(crystal);
            RemoveCollider(shard);
            RemoveCollider(shard2);
            RemoveCollider(basePlate);

            return root;
        }

        private static void CreateHPText(GameObject root)
        {
            // HP text container (this will billboard to face camera)
            GameObject hpTextContainer = new GameObject("HPTextContainer");
            hpTextContainer.transform.SetParent(root.transform);
            hpTextContainer.transform.localPosition = new Vector3(0f, 1.5f, 0f); // Above resource node
            hpTextContainer.AddComponent<Billboard>();

            // Create TextMesh for HP number
            GameObject hpTextObj = new GameObject("HPText");
            hpTextObj.transform.SetParent(hpTextContainer.transform);
            hpTextObj.transform.localPosition = Vector3.zero;
            hpTextObj.transform.localRotation = Quaternion.identity;

            TextMesh textMesh = hpTextObj.AddComponent<TextMesh>();
            textMesh.text = "10";
            textMesh.characterSize = 0.12f;
            textMesh.fontSize = 52;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.color = Color.white;
            textMesh.fontStyle = FontStyle.Bold;

            // Add black outline by creating a slightly offset shadow text
            GameObject shadowObj = new GameObject("HPTextShadow");
            shadowObj.transform.SetParent(hpTextContainer.transform);
            shadowObj.transform.localPosition = new Vector3(0.02f, -0.02f, 0.01f);
            shadowObj.transform.localRotation = Quaternion.identity;

            TextMesh shadowMesh = shadowObj.AddComponent<TextMesh>();
            shadowMesh.text = "10";
            shadowMesh.characterSize = 0.12f;
            shadowMesh.fontSize = 52;
            shadowMesh.anchor = TextAnchor.MiddleCenter;
            shadowMesh.alignment = TextAlignment.Center;
            shadowMesh.color = Color.black;
            shadowMesh.fontStyle = FontStyle.Bold;
        }

        private static void SetColor(GameObject obj, Color color)
        {
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                // Use Unlit/Color shader which is guaranteed to have _Color property
                Shader shader = Shader.Find("Unlit/Color");
                if (shader == null)
                {
                    shader = Shader.Find("Standard"); // Fallback
                }

                renderer.material = new Material(shader);
                renderer.material.color = color;
            }
        }

        private static Color LightenColor(Color color, float amount)
        {
            return new Color(
                Mathf.Min(1f, color.r + amount),
                Mathf.Min(1f, color.g + amount),
                Mathf.Min(1f, color.b + amount),
                color.a
            );
        }

        private static Color DarkenColor(Color color, float factor)
        {
            return new Color(color.r * factor, color.g * factor, color.b * factor, color.a);
        }

        private static void RemoveCollider(GameObject obj)
        {
            Collider col = obj.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
        }
    }

    /// <summary>
    /// Makes a transform always face the camera on the Y axis (yaw only).
    /// Used for world-space HP bars.
    /// </summary>
    public class BillboardY : MonoBehaviour
    {
        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 lookDir = cam.transform.position - transform.position;
            lookDir.y = 0;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(-lookDir, Vector3.up);
            }
        }
    }
}
