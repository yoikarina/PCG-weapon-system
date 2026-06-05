#if UNITY_EDITOR
// Custom Inspector for GunBodyData; provides tag dropdown and highlights invalid tags.

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

            // Auto-find TagDefinitions
            if (_tagDefs == null)
            {
                var guids = AssetDatabase.FindAssets("t:TagDefinitions");
                if (guids.Length > 0)
                    _tagDefs = AssetDatabase.LoadAssetAtPath<TagDefinitions>(
                        AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            // ── Basic info ────────────────────────────────────────────────────
            EditorGUILayout.LabelField("Basic Info", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("bodyId"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"));

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Scene Model", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("bodyPrefab"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("partObject"));

            // ── Tags ──────────────────────────────────────────────────────────
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Compatibility Tags", EditorStyles.boldLabel);

            var tagsProp = serializedObject.FindProperty("tags");
            for (int i = 0; i < tagsProp.arraySize; i++)
            {
                var tagVal  = tagsProp.GetArrayElementAtIndex(i).stringValue;
                bool isValid = _tagDefs == null || _tagDefs.IsValid(tagVal);

                using (new EditorGUILayout.HorizontalScope())
                {
                    var prev = GUI.color;
                    if (!isValid) GUI.color = new Color(1f, 0.5f, 0.4f);
                    EditorGUILayout.LabelField(
                        isValid ? $"• {tagVal}" : $"⚠ {tagVal}  (not in TagDefinitions)");
                    GUI.color = prev;

                    if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(22)))
                    {
                        tagsProp.DeleteArrayElementAtIndex(i);
                        serializedObject.ApplyModifiedProperties();
                        return;
                    }
                }
            }

            EditorGUILayout.Space(2);
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
                        if (GUILayout.Button("Add", EditorStyles.miniButton, GUILayout.Width(44)))
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
                    EditorGUILayout.LabelField("All available tags added.",
                        EditorStyles.centeredGreyMiniLabel);
                }
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "TagDefinitions not found.\n" +
                    "Create → GunAssemblyTool → Tag Definitions to enable dropdown.",
                    MessageType.Info);
                EditorGUILayout.PropertyField(tagsProp, true);
            }

            // ── Slots ─────────────────────────────────────────────────────────
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Available Slots", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Each slot can optionally restrict physical interface:\n" +
                "• allowedThreads — which thread types fit (Barrel/Muzzle)\n" +
                "• allowedMags    — which magazine formats fit (Magazine)\n" +
                "Leave lists empty for no restriction.",
                MessageType.None);

            var slotsProp = serializedObject.FindProperty("slots");
            if (slotsProp != null)
                EditorGUILayout.PropertyField(slotsProp, true);

            // ── Stats ─────────────────────────────────────────────────────────
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Stats", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Base stat values for this gun body.\n" +
                "Attachments stack their own stat values on top of these.",
                MessageType.None);

            var statsProp = serializedObject.FindProperty("stats");
            if (statsProp != null)
                EditorGUILayout.PropertyField(statsProp, true);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif