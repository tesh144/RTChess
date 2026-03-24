#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using UnityEditor;
using LittleCafe;
using ClockworkCraft;

namespace LittleCafe.Editor
{
    /// <summary>
    /// Property drawers that replace "Element 0", "Element 1" etc. with the
    /// entry's asset/currency name in all database ScriptableObject inspectors.
    /// </summary>

    [CustomPropertyDrawer(typeof(BuildingData))]
    public class BuildingDataDrawer : NamedEntryDrawer
    {
        protected override string NameField => "assetName";
    }

    [CustomPropertyDrawer(typeof(EnvironmentData))]
    public class EnvironmentDataDrawer : NamedEntryDrawer
    {
        protected override string NameField => "assetName";
    }

    [CustomPropertyDrawer(typeof(UnitData))]
    public class UnitDataDrawer : NamedEntryDrawer
    {
        protected override string NameField => "assetName";
    }

    [CustomPropertyDrawer(typeof(WorkerData))]
    public class WorkerDataDrawer : NamedEntryDrawer
    {
        protected override string NameField => "assetName";
    }

    [CustomPropertyDrawer(typeof(FurnitureData))]
    public class FurnitureDataDrawer : NamedEntryDrawer
    {
        protected override string NameField => "assetName";
    }

    [CustomPropertyDrawer(typeof(CurrencyData))]
    public class CurrencyDataDrawer : NamedEntryDrawer
    {
        protected override string NameField => "currencyName";
    }

    /// <summary>
    /// Base drawer — shows the name field value as the foldout label,
    /// then draws all child properties when expanded.
    /// </summary>
    public abstract class NamedEntryDrawer : PropertyDrawer
    {
        protected abstract string NameField { get; }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
                return EditorGUIUtility.singleLineHeight;

            float height = EditorGUIUtility.singleLineHeight + 2f; // foldout row
            var iter = property.Copy();
            var end = iter.GetEndProperty();
            iter.NextVisible(true); // enter children
            while (!SerializedProperty.EqualContents(iter, end))
            {
                height += EditorGUI.GetPropertyHeight(iter, true) + 2f;
                if (!iter.NextVisible(false)) break;
            }
            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Replace "Element N" with the entry's name
            var nameProp = property.FindPropertyRelative(NameField);
            string displayName = (nameProp != null && !string.IsNullOrEmpty(nameProp.stringValue))
                ? nameProp.stringValue
                : label.text;

            EditorGUI.BeginProperty(position, label, property);

            var foldoutRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, displayName, true);

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                float y = position.y + EditorGUIUtility.singleLineHeight + 2f;

                var iter = property.Copy();
                var end = iter.GetEndProperty();
                iter.NextVisible(true);
                while (!SerializedProperty.EqualContents(iter, end))
                {
                    float h = EditorGUI.GetPropertyHeight(iter, true);
                    var rect = new Rect(position.x, y, position.width, h);
                    EditorGUI.PropertyField(rect, iter, true);
                    y += h + 2f;
                    if (!iter.NextVisible(false)) break;
                }
                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }
    }
}
