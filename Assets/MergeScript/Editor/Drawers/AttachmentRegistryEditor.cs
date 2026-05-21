#if UNITY_EDITOR
// Adds an "Auto Collect" button to the AttachmentRegistry Inspector to scan the project and register all part assets.

using System.Collections.Generic;
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
            EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel);

            // Primary collect button.
            // Searches every known concrete subclass by name, then falls back to
            // loading every ScriptableObject in the project and filtering by type.
            // This two-pass approach handles the Unity limitation where abstract base
            // class searches return nothing, and same-file subclass searches are
            // sometimes missed by the asset database index.
            if (GUILayout.Button("Auto Collect All Parts From Project", GUILayout.Height(30)))
            {
                var registry = (AttachmentRegistry)target;
                var found    = CollectAllAttachments();

                registry.allAttachments = found;
                EditorUtility.SetDirty(registry);
                AssetDatabase.SaveAssets();

                Debug.Log(
                    $"[AttachmentRegistry] Collected {found.Count} parts:\n" +
                    string.Join("\n", found.Select(a =>
                        $"  · {a.attachmentId} ({a.GetType().Name} / {a.attachType})")));
            }

            EditorGUILayout.Space(4);

            // Debug button — logs every ScriptableObject found in the project
            // so you can verify your assets exist and are visible to the asset database.
            if (GUILayout.Button("Debug: List All ScriptableObjects", GUILayout.Height(24)))
            {
                var allGuids = AssetDatabase.FindAssets("t:ScriptableObject");
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Found {allGuids.Length} ScriptableObjects:");
                foreach (var g in allGuids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(g);
                    var obj  = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                    if (obj != null)
                        sb.AppendLine($"  {obj.GetType().Name} — {path}");
                }
                Debug.Log(sb.ToString());
            }

            EditorGUILayout.Space(4);

            var r = (AttachmentRegistry)target;
            if (r.allAttachments != null && r.allAttachments.Count > 0)
                EditorGUILayout.HelpBox(
                    $"{r.allAttachments.Count} parts registered.", MessageType.Info);
            else
                EditorGUILayout.HelpBox(
                    "List is empty. Create part assets then click Auto Collect.",
                    MessageType.Warning);
        }

        // ── Collection logic ──────────────────────────────────────────────────

        private static List<AttachmentData> CollectAllAttachments()
        {
            var result = new List<AttachmentData>();

            // Pass 1: search each concrete subclass type name individually.
            // Works when each class is in its own file or the asset database
            // has fully indexed the project.
            var typeNames = new[]
            {
                "t:BarrelData", "t:MagazineData", "t:MuzzleData",
                "t:ScopeData",  "t:StockData",    "t:GripData",
                "t:UnderbarrelData", "t:SkinData"
            };

            var seenGuids = new HashSet<string>();
            foreach (var typeName in typeNames)
            {
                foreach (var guid in AssetDatabase.FindAssets(typeName))
                {
                    if (!seenGuids.Add(guid)) continue;
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var asset = AssetDatabase.LoadAssetAtPath<AttachmentData>(path);
                    if (asset != null) result.Add(asset);
                }
            }

            // Pass 2: fallback — load every ScriptableObject and filter by type.
            // Catches assets missed by Pass 1 due to asset database indexing issues
            // with abstract base classes or multi-class files.
            foreach (var guid in AssetDatabase.FindAssets("t:ScriptableObject"))
            {
                if (seenGuids.Contains(guid)) continue;
                var path  = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<AttachmentData>(path);
                if (asset != null)
                {
                    seenGuids.Add(guid);
                    result.Add(asset);
                }
            }

            return result;
        }
    }
}
#endif