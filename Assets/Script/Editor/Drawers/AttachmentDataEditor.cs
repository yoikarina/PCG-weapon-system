#if UNITY_EDITOR
// Custom Inspector for AttachmentData subclasses; hides the locked attachType field and shows a read-only label instead.

using UnityEditor;
using UnityEngine;

namespace GunAssemblyTool.Editor
{
    // Applied to AttachmentData and all subclasses via editorForChildClasses: true.
    [CustomEditor(typeof(AttachmentData), editorForChildClasses: true)]
    public class AttachmentDataEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var data = (AttachmentData)target;

            // Determine whether this is a concrete subclass whose attachType
            // is locked by OnValidate. All known subclasses are listed here;
            // add new ones when additional slot types are introduced.
            bool isLocked =
                data is BarrelData      || data is MagazineData ||
                data is MuzzleData      || data is ScopeData    ||
                data is StockData       || data is GripData     ||
                data is UnderbarrelData || data is SkinData;

            if (isLocked)
            {
                // Display the slot type as a disabled read-only label.
                // This communicates the locked value to designers without
                // exposing an editable field that would be overridden on save.
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.LabelField("Slot Type", data.attachType.ToString(), EditorStyles.boldLabel);
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.HelpBox(
                    $"Slot type is locked to \"{data.attachType}\" for this part type.",
                    MessageType.None);

                EditorGUILayout.Space(4);
            }

            // Draw every serialised field except m_Script (Unity internal) and
            // attachType (already shown as a read-only label above for locked types).
            var prop           = serializedObject.GetIterator();
            bool enterChildren = true;
            while (prop.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (prop.name == "m_Script")                    continue;
                if (isLocked && prop.name == "attachType")      continue;
                EditorGUILayout.PropertyField(prop, true);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
