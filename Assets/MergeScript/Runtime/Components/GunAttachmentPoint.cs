// Mount point component placed on gun prefab child nodes to mark where attachments appear.

using UnityEngine;

namespace GunAssemblyTool
{
    // Add one of these to a child GameObject for each slot the gun body exposes.
    // Each slot type must use a separate GameObject
    // (enforced by DisallowMultipleComponent).
    [AddComponentMenu("GunAssemblyTool/Gun Attachment Point")]
    [DisallowMultipleComponent]
    public class GunAttachmentPoint : MonoBehaviour
    {
        // The slot type this mount point represents.
        // Must match a slot defined in the gun body's GunBodyData.slots list.
        public AttachmentType slotType;

        // Fine-tune offset applied to the spawned attachment prefab's local position.
        // Use this when the attachment model's pivot does not align with this transform.
        [Tooltip("Local position offset applied to the spawned attachment prefab.")]
        public Vector3 localPositionOffset = Vector3.zero;

        // Fine-tune offset applied to the spawned attachment prefab's local rotation.
        [Tooltip("Local rotation offset (Euler angles) applied to the spawned attachment prefab.")]
        public Vector3 localRotationOffset = Vector3.zero;

        // Reference to the attachment GameObject currently parented to this mount point.
        // Managed internally by Attach and Detach; hidden in the Inspector to prevent
        // accidental modification.
        [HideInInspector]
        public GameObject currentAttachmentInstance;

        // Instantiates data.attachmentPrefab as a child of this transform and applies
        // the local position and rotation offsets. Calls Detach first to ensure any
        // previously attached model is destroyed before the new one is created.
        // Does nothing if data is null or data.attachmentPrefab is null.
        public void Attach(AttachmentData data)
        {
            Detach();
            if (data == null || data.attachmentPrefab == null) return;
            currentAttachmentInstance = Instantiate(data.attachmentPrefab, transform);
            currentAttachmentInstance.transform.localPosition = localPositionOffset;
            currentAttachmentInstance.transform.localRotation = Quaternion.Euler(localRotationOffset);
        }

        // Destroys the currently attached model and clears the reference.
        // Safe to call when the slot is already empty.
        public void Detach()
        {
            if (currentAttachmentInstance != null)
            {
                Destroy(currentAttachmentInstance);
                currentAttachmentInstance = null;
            }
        }

#if UNITY_EDITOR
        // Draws a cyan wire sphere at this transform's position in the Scene view
        // so designers can see mount point locations without entering Play mode.
        // Also draws a text label showing the slot type.
        // Editor-only — stripped from runtime builds.
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, 0.03f);
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 0.05f,
                slotType.ToString(),
                new GUIStyle { fontSize = 9, normal = { textColor = Color.cyan } });
        }
#endif
    }
}
