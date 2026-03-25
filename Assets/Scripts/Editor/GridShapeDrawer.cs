#pragma warning disable CS0414, CS0219, CS0618
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using ClockworkGrid;

/// <summary>
/// Custom PropertyDrawer for GridShape.
/// Renders a 7×7 clickable toggle grid in the Inspector.
///
/// - Orange cell at (0,0) = anchor; can never be deselected.
/// - Blue cells = selected/occupied.
/// - Dark grey cells = empty.
/// - Preset buttons: 1×1, 2×1, 1×2, 2×2, 3×3, Clear.
/// - All edits are Undo-recordable.
/// </summary>
[CustomPropertyDrawer(typeof(GridShape))]
public class GridShapeDrawer : PropertyDrawer
{
    private const int GridDim      = 7;    // 7×7 grid
    private const int CellPixels   = 22;   // cell size in pixels
    private const int CellPad      = 2;    // gap between cells
    private const int ButtonH      = 20;   // height of preset buttons
    private const int GridHeaderH  = 4;    // spacing between foldout and grid
    private const int ButtonPad    = 4;    // spacing between grid and buttons

    // Total height of the expanded grid area (grid rows + buttons + spacing)
    private float ExpandedHeight =>
        EditorGUIUtility.singleLineHeight   // foldout header
        + GridHeaderH
        + GridDim * (CellPixels + CellPad)  // grid rows
        + ButtonPad
        + ButtonH                           // preset buttons
        + 4;                                // bottom padding

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return property.isExpanded
            ? ExpandedHeight
            : EditorGUIUtility.singleLineHeight;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        // ── Foldout header ─────────────────────────────────────────────────
        Rect headerRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

        SerializedProperty offsetsProp = property.FindPropertyRelative("cellOffsets");
        int cellCount = offsetsProp != null ? offsetsProp.arraySize : 0;
        GUIContent labelWithCount = new GUIContent(label.text + $"  ({cellCount} cells)", label.tooltip);

        property.isExpanded = EditorGUI.Foldout(headerRect, property.isExpanded, labelWithCount, true);

        if (!property.isExpanded)
        {
            EditorGUI.EndProperty();
            return;
        }

        if (offsetsProp == null)
        {
            EditorGUI.HelpBox(
                new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight + GridHeaderH, position.width, 32),
                "GridShape: cellOffsets not found", MessageType.Error);
            EditorGUI.EndProperty();
            return;
        }

        // ── Build occupied cell lookup ─────────────────────────────────────
        HashSet<Vector2Int> occupied = new HashSet<Vector2Int>();
        for (int i = 0; i < offsetsProp.arraySize; i++)
            occupied.Add(offsetsProp.GetArrayElementAtIndex(i).vector2IntValue);

        // ── Draw grid ──────────────────────────────────────────────────────
        float gridTop = position.y + EditorGUIUtility.singleLineHeight + GridHeaderH;
        int indentLevel = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;  // suppress indent inside grid

        for (int row = 0; row < GridDim; row++)
        {
            // row 0 = top of grid = highest y value
            int gridY = GridDim - 1 - row;

            for (int gridX = 0; gridX < GridDim; gridX++)
            {
                Rect cellRect = new Rect(
                    position.x + gridX * (CellPixels + CellPad),
                    gridTop + row * (CellPixels + CellPad),
                    CellPixels,
                    CellPixels
                );

                bool isAnchor   = (gridX == 0 && gridY == 0);
                bool isOccupied = occupied.Contains(new Vector2Int(gridX, gridY));

                // Colour coding
                Color bg = isAnchor   ? new Color(1.0f, 0.55f, 0.15f) :
                           isOccupied ? new Color(0.25f, 0.60f, 1.0f) :
                                        new Color(0.22f, 0.22f, 0.22f);

                Color savedBg = GUI.backgroundColor;
                GUI.backgroundColor = bg;
                bool clicked = GUI.Button(cellRect, GUIContent.none);
                GUI.backgroundColor = savedBg;

                // Anchor label
                if (isAnchor)
                {
                    GUIStyle anchorStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontStyle = FontStyle.Bold
                    };
                    anchorStyle.normal.textColor = Color.black;
                    GUI.Label(cellRect, "A", anchorStyle);
                }

                // Handle click
                if (clicked && !isAnchor)
                {
                    Undo.RecordObject(property.serializedObject.targetObject, "Edit GridShape");
                    if (isOccupied)
                        RemoveOffset(offsetsProp, new Vector2Int(gridX, gridY));
                    else
                        AddOffset(offsetsProp, new Vector2Int(gridX, gridY));
                    property.serializedObject.ApplyModifiedProperties();
                    GUI.changed = true;
                }
            }
        }

        EditorGUI.indentLevel = indentLevel;

        // ── Preset buttons ────────────────────────────────────────────────
        float btnTop = gridTop + GridDim * (CellPixels + CellPad) + ButtonPad;

        string[] presetLabels = { "1x1", "2x1", "1x2", "2x2", "3x3", "Clear" };
        int[,] presetSizes    = { {1,1}, {2,1}, {1,2}, {2,2}, {3,3}, {1,1} };

        float bw = 38f;
        float bpad = 3f;

        for (int i = 0; i < presetLabels.Length; i++)
        {
            Rect btnRect = new Rect(position.x + i * (bw + bpad), btnTop, bw, ButtonH);
            if (GUI.Button(btnRect, presetLabels[i]))
            {
                Undo.RecordObject(property.serializedObject.targetObject, $"GridShape Preset {presetLabels[i]}");
                SetRectangularShape(offsetsProp, presetSizes[i, 0], presetSizes[i, 1]);
                property.serializedObject.ApplyModifiedProperties();
                GUI.changed = true;
            }
        }

        EditorGUI.EndProperty();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static void AddOffset(SerializedProperty offsetsProp, Vector2Int v)
    {
        for (int i = 0; i < offsetsProp.arraySize; i++)
            if (offsetsProp.GetArrayElementAtIndex(i).vector2IntValue == v) return;

        offsetsProp.InsertArrayElementAtIndex(offsetsProp.arraySize);
        offsetsProp.GetArrayElementAtIndex(offsetsProp.arraySize - 1).vector2IntValue = v;
    }

    private static void RemoveOffset(SerializedProperty offsetsProp, Vector2Int v)
    {
        for (int i = offsetsProp.arraySize - 1; i >= 0; i--)
        {
            if (offsetsProp.GetArrayElementAtIndex(i).vector2IntValue == v)
            {
                offsetsProp.DeleteArrayElementAtIndex(i);
                return;
            }
        }
    }

    /// <summary>
    /// Clears the offset list and fills it with a w×h rectangle.
    /// Always includes the anchor (0,0).
    /// </summary>
    private static void SetRectangularShape(SerializedProperty offsetsProp, int w, int h)
    {
        offsetsProp.ClearArray();
        for (int x = 0; x < Mathf.Max(1, w); x++)
        {
            for (int y = 0; y < Mathf.Max(1, h); y++)
            {
                offsetsProp.InsertArrayElementAtIndex(offsetsProp.arraySize);
                offsetsProp.GetArrayElementAtIndex(offsetsProp.arraySize - 1).vector2IntValue = new Vector2Int(x, y);
            }
        }
    }
}
