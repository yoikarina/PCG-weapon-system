#if UNITY_EDITOR
// Custom Inspector for GunBodyData; provides a tag dropdown backed by TagDefinitions and highlights invalid tags.

using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GunAssemblyTool.Editor
{
    [CustomEditor(typeof(GunBodyData))]
    public class GunBodyDataEditor : UnityEditor.Editor
    {
        // Cached reference to the TagDefinitions asset found in the project.
        // Located automatically in OnInspectorGUI; null until the asset is found.
        private TagDefinitions _tagDefs;

        // Index into the available-tag dropdown used for the "Add" button.
        private int _addTagIdx;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var body = (GunBodyData)target;

            // Attempt to locate the TagDefinitions asset on first draw.
            // The result is cached so FindAssets is not called every frame.
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

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Scene Model", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("bodyPrefab"));

            // ── Compatibility tags ────────────────────────────────────────────
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Compatibility Tags", EditorStyles.boldLabel);

            var tagsProp = serializedObject.FindProperty("tags");

            // Draw each current tag with a remove button.
            // Tags not found in TagDefinitions are tinted red to alert the designer.
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
                        return; // Exit to avoid iterating a modified array
                    }
                }
            }

            EditorGUILayout.Space(2);

            if (_tagDefs != null)
            {
                // Build the list of tags not already on this body so the dropdown
                // only shows tags that can still be added.
                var available = _tagDefs.AllTags
                    .Where(t => !body.tags.Contains(t))
                    .ToArray();

                if (available.Length > 0)
                {
                    // Render a dropdown and an Add button on the same row.
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
                    EditorGUILayout.LabelField(
                        "All available tags added.", EditorStyles.centeredGreyMiniLabel);
                }
            }
            else
            {
                // Fall back to the default array field when TagDefinitions is absent.
                EditorGUILayout.HelpBox(
                    "TagDefinitions asset not found.\n" +
                    "Create → GunAssemblyTool → Tag Definitions to enable the dropdown.",
                    MessageType.Info);
                EditorGUILayout.PropertyField(tagsProp, true);
            }

            // ── Base stats ────────────────────────────────────────────────────
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Base Stats", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("baseDamage"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("baseFireRate"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("baseAccuracy"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("baseReloadTime"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("baseAmmoCapacity"));

            // ── Available slots ───────────────────────────────────────────────
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Available Slots", EditorStyles.boldLabel);

            // Remind the designer how the physical interface fields work.
            EditorGUILayout.HelpBox(
                "Each slot can optionally restrict physical interface:\n" +
                "• allowedThreads — which thread types fit this slot (Barrel / Muzzle)\n" +
                "• allowedMags    — which magazine formats fit this slot (Magazine)\n" +
                "Leave both lists empty for no physical restriction.",
                MessageType.None);

            EditorGUILayout.PropertyField(serializedObject.FindProperty("slots"), true);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
