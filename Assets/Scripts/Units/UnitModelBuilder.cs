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

            // Add HP bar above unit
            CreateHPBar(root);

            return root;
        }

        private static void CreateHPBar(GameObject root)
        {
            // HP bar background
            GameObject hpBarBG = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hpBarBG.name = "HPBarBG";
            hpBarBG.transform.SetParent(root.transform);
            hpBarBG.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            hpBarBG.transform.localScale = new Vector3(0.6f, 0.08f, 0.05f);
            SetColor(hpBarBG, new Color(0.2f, 0.2f, 0.2f));
            RemoveCollider(hpBarBG);

            // HP bar fill (starts full)
            GameObject hpBarFill = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hpBarFill.name = "HPBarFill";
            hpBarFill.transform.SetParent(hpBarBG.transform);
            hpBarFill.transform.localPosition = new Vector3(0f, 0f, 0.01f);
            hpBarFill.transform.localScale = new Vector3(1f, 0.8f, 1.2f);
            SetColor(hpBarFill, Color.green);
            RemoveCollider(hpBarFill);
        }

        private static void SetColor(GameObject obj, Color color)
        {
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = new Material(Shader.Find("Standard"));
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
