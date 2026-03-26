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
    /// Custom drawer for POITypeData — renders typeName as a dropdown populated
    /// from the appropriate database (Environment/Unit/Building) based on sourceType.
    /// </summary>
    [CustomPropertyDrawer(typeof(POITypeData))]
    public class POITypeDataDrawer : PropertyDrawer
    {
        // Cached database lookups
        private static EnvironmentDatabase s_envDB;
        private static UnitDatabase s_unitDB;
        private static BuildingDatabase s_buildingDB;

        private static T FindDB<T>() where T : ScriptableObject
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            if (guids.Length > 0)
                return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
            return null;
        }

        private static string[] GetAssetNames(POISourceType sourceType)
        {
            switch (sourceType)
            {
                case POISourceType.Environment:
                    if (s_envDB == null) s_envDB = FindDB<EnvironmentDatabase>();
                    if (s_envDB != null)
                        return s_envDB.AllEnvironment.ConvertAll(e => e.assetName).ToArray();
                    break;
                case POISourceType.Unit:
                    if (s_unitDB == null) s_unitDB = FindDB<UnitDatabase>();
                    if (s_unitDB != null)
                        return s_unitDB.AllUnits.ConvertAll(u => u.assetName).ToArray();
                    break;
                case POISourceType.Building:
                    if (s_buildingDB == null) s_buildingDB = FindDB<BuildingDatabase>();
                    if (s_buildingDB != null)
                        return s_buildingDB.AllBuildings.ConvertAll(b => b.assetName).ToArray();
                    break;
            }
            return new string[0];
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
                return EditorGUIUtility.singleLineHeight;

            float height = EditorGUIUtility.singleLineHeight + 2f;
            var iter = property.Copy();
            var end = iter.GetEndProperty();
            iter.NextVisible(true);
            while (!SerializedProperty.EqualContents(iter, end))
            {
                height += EditorGUI.GetPropertyHeight(iter, true) + 2f;
                if (!iter.NextVisible(false)) break;
            }
            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Use label field as foldout name
            var labelProp = property.FindPropertyRelative("label");
            string displayName = (labelProp != null && !string.IsNullOrEmpty(labelProp.stringValue))
                ? labelProp.stringValue
                : label.text;

            EditorGUI.BeginProperty(position, label, property);

            var foldoutRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, displayName, true);

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                float y = position.y + EditorGUIUtility.singleLineHeight + 2f;

                var sourceTypeProp = property.FindPropertyRelative("sourceType");
                var typeNameProp = property.FindPropertyRelative("typeName");

                var iter = property.Copy();
                var end = iter.GetEndProperty();
                iter.NextVisible(true);
                while (!SerializedProperty.EqualContents(iter, end))
                {
                    float h = EditorGUI.GetPropertyHeight(iter, true);
                    var rect = new Rect(position.x, y, position.width, h);

                    // Render typeName as a dropdown from the source database
                    if (iter.name == "typeName" && sourceTypeProp != null && typeNameProp != null)
                    {
                        var sourceType = (POISourceType)sourceTypeProp.enumValueIndex;
                        string[] names = GetAssetNames(sourceType);

                        if (names.Length > 0)
                        {
                            int currentIndex = System.Array.IndexOf(names, typeNameProp.stringValue);
                            if (currentIndex < 0) currentIndex = 0;

                            int newIndex = EditorGUI.Popup(rect, "Type Name", currentIndex, names);
                            if (newIndex >= 0 && newIndex < names.Length)
                                typeNameProp.stringValue = names[newIndex];
                        }
                        else
                        {
                            EditorGUI.PropertyField(rect, iter, true);
                        }
                    }
                    else
                    {
                        EditorGUI.PropertyField(rect, iter, true);
                    }

                    y += h + 2f;
                    if (!iter.NextVisible(false)) break;
                }
                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }
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
