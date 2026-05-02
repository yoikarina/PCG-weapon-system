#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GunAssemblyTool.Editor
{
    [CustomEditor(typeof(GunBodyData))]
    public class GunBodyDataEditor : UnityEditor.Editor
    {
        private TagDefinitions _tagDefs;
        private int            _addTagIdx;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var body = (GunBodyData)target;

            // auto search TagDefinitions
            if (_tagDefs == null)
            {
                var guids = AssetDatabase.FindAssets("t:TagDefinitions");
                if (guids.Length > 0)
                    _tagDefs = AssetDatabase.LoadAssetAtPath<TagDefinitions>(
                        AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            // basic info
            EditorGUILayout.LabelField("Basic info", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("bodyId"));

            
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Model", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("bodyPrefab"));

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Tags", EditorStyles.boldLabel);

            var tagsProp = serializedObject.FindProperty("tags");

            // show tags for now
            for (int i = 0; i < tagsProp.arraySize; i++)
            {
                var tagVal = tagsProp.GetArrayElementAtIndex(i).stringValue;
                bool isValid = _tagDefs == null || _tagDefs.IsValid(tagVal);

                using (new EditorGUILayout.HorizontalScope())
                {
                    var prevColor = GUI.color;
                    if (!isValid) GUI.color = new Color(1f, 0.5f, 0.4f);
                    EditorGUILayout.LabelField(isValid ? $"• {tagVal}" : $"⚠ {tagVal} (not TagDefinitions in)");
                    GUI.color = prevColor;

                    if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(22)))
                    {
                        tagsProp.DeleteArrayElementAtIndex(i);
                        serializedObject.ApplyModifiedProperties();
                        return;
                    }
                }
            }

            EditorGUILayout.Space(2);

            // add tags
            if (_tagDefs != null)
            {
                var available = _tagDefs.AllTags
                    .Where(t => !body.tags.Contains(t))
                    .ToArray();

                if (available.Length > 0)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        _addTagIdx = Mathf.Clamp(_addTagIdx, 0, available.Length - 1);
                        _addTagIdx = EditorGUILayout.Popup(_addTagIdx, available);
                        if (GUILayout.Button("add", EditorStyles.miniButton, GUILayout.Width(44)))
                        {
                            tagsProp.arraySize++;
                            tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1)
                                    .stringValue = available[_addTagIdx];
                            _addTagIdx = 0;
                        }
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("all available tags have been added", EditorStyles.centeredGreyMiniLabel);
                }
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "not find TagDefinitions assets。\n" +
                    "Create → GunAssemblyTool → Tag Definitions auto enable。",
                    MessageType.Info);
                EditorGUILayout.PropertyField(tagsProp, true);
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("basic attributes", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("baseDamage"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("baseFireRate"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("baseAccuracy"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("baseReloadTime"));

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("available slot", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("availableSlots"), true);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
