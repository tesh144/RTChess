using UnityEngine;

namespace ClockworkGrid
{
    /// <summary>
    /// Builds a simple 3D soldier model from primitives at runtime.
    /// Creates a capsule body with a cone-like front indicator and a shield on the back,
    /// making facing direction immediately obvious from top-down view.
    /// </summary>
    public static class UnitModelBuilder
    {
        public static GameObject CreateSoldierModel(Color teamColor)
        {
            GameObject root = new GameObject("SoldierModel");

            // Body - capsule
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.transform.SetParent(root.transform);
            body.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            body.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            SetColor(body, teamColor);

            // Front indicator - small cube pointing forward (+Z = North at start)
            // This is the "weapon" that shows which way the unit faces
            GameObject weapon = GameObject.CreatePrimitive(PrimitiveType.Cube);
            weapon.transform.SetParent(root.transform);
            weapon.transform.localPosition = new Vector3(0f, 0.5f, 0.35f);
            weapon.transform.localScale = new Vector3(0.12f, 0.12f, 0.4f);
            SetColor(weapon, Color.white);

            // Arrow head - flattened cube at the tip to make a clear pointer
            GameObject arrowHead = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arrowHead.transform.SetParent(root.transform);
            arrowHead.transform.localPosition = new Vector3(0f, 0.5f, 0.6f);
            arrowHead.transform.localScale = new Vector3(0.3f, 0.1f, 0.15f);
            arrowHead.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            SetColor(arrowHead, Color.white);

            // Back indicator - flat rectangle "shield" on back side
            GameObject shield = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shield.transform.SetParent(root.transform);
            shield.transform.localPosition = new Vector3(0f, 0.5f, -0.3f);
            shield.transform.localScale = new Vector3(0.4f, 0.4f, 0.06f);
            SetColor(shield, DarkenColor(teamColor, 0.5f));

            // Remove colliders from visual parts (we'll use the root for raycasting)
            RemoveCollider(body);
            RemoveCollider(weapon);
            RemoveCollider(arrowHead);
            RemoveCollider(shield);

            // Add HP text above unit
            CreateHPText(root);

            return root;
        }

        private static void CreateHPText(GameObject root)
        {
            // HP text container (this will billboard to face camera)
            GameObject hpTextContainer = new GameObject("HPTextContainer");
            hpTextContainer.transform.SetParent(root.transform);
            hpTextContainer.transform.localPosition = new Vector3(0f, 1.2f, 0f); // Top center above unit
            hpTextContainer.AddComponent<Billboard>();

            // Create TextMesh for HP number
            GameObject hpTextObj = new GameObject("HPText");
            hpTextObj.transform.SetParent(hpTextContainer.transform);
            hpTextObj.transform.localPosition = Vector3.zero;
            hpTextObj.transform.localRotation = Quaternion.identity;

            TextMesh textMesh = hpTextObj.AddComponent<TextMesh>();
            textMesh.text = "10";
            textMesh.characterSize = 0.1f;
            textMesh.fontSize = 48;
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
            shadowMesh.characterSize = 0.1f;
            shadowMesh.fontSize = 48;
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
}
