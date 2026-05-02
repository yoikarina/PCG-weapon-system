#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GunAssemblyTool.Editor
{
    [CustomEditor(typeof(AttachmentRegistry))]
    public class AttachmentRegistryEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Tool", EditorStyles.boldLabel);

            if (GUILayout.Button("Automatically collect all AttachmentData within the Project", GUILayout.Height(30)))
            {
                var registry = (AttachmentRegistry)target;

                var guids = AssetDatabase.FindAssets("t:AttachmentData");
                var found = guids
                    .Select(g => AssetDatabase.LoadAssetAtPath<AttachmentData>(
                        AssetDatabase.GUIDToAssetPath(g)))
                    .Where(a => a != null)
                    .ToList();

                registry.allAttachments = found;
                EditorUtility.SetDirty(registry);
                AssetDatabase.SaveAssets();

                Debug.Log($"[AttachmentRegistry] Collection complete，total {found.Count} accessories：\n" +
                          string.Join("\n", found.Select(a => $"  · {a.attachmentId} ({a.attachType})")));
            }

            EditorGUILayout.Space(4);
            var r = (AttachmentRegistry)target;
            if (r.allAttachments != null && r.allAttachments.Count > 0)
                EditorGUILayout.HelpBox($"Registered {r.allAttachments.Count} accessories。", MessageType.Info);
            else
                EditorGUILayout.HelpBox("The list is empty. After creating AttachmentData, click the button above to collect it.。", MessageType.Warning);
        }
    }
}
#endif
