using UnityEngine;

namespace GunAssemblyTool
{
    // Defines where an attachment should appear on the gun prefab
    // Add this to the gun model at the correct position
    [AddComponentMenu("GunAssemblyTool/Gun Attachment Point")]
    [DisallowMultipleComponent]
    public class GunAttachmentPoint : MonoBehaviour
    {
        // Which slot this point represents (e.g. scope, barrel)
        [Tooltip("Which attachment slot this point represents")]
        public AttachmentType slotType;

        // Position offset for the attachment (relative to this transform)
        [Tooltip("Local position offset for the attachment")]
        public Vector3 localPositionOffset = Vector3.zero;

        // Rotation offset (Euler angles)
        [Tooltip("Local rotation offset for the attachment")]
        public Vector3 localRotationOffset = Vector3.zero;

        // Current instantiated attachment object
        [HideInInspector]
        public GameObject currentAttachmentInstance;

        /// <summary>
        /// Attach an attachment:
        /// Instantiates the prefab and parents it to this point
        /// </summary>
        public void Attach(AttachmentData data)
        {
            // Remove existing one first
            Detach();

            if (data == null || data.attachmentPrefab == null) return;

            currentAttachmentInstance = Instantiate(data.attachmentPrefab, transform);

            currentAttachmentInstance.transform.localPosition = localPositionOffset;
            currentAttachmentInstance.transform.localRotation = Quaternion.Euler(localRotationOffset);
        }

        /// <summary>
        /// Remove current attachment instance
        /// </summary>
        public void Detach()
        {
            if (currentAttachmentInstance != null)
            {
                Destroy(currentAttachmentInstance);
                currentAttachmentInstance = null;
            }
        }

#if UNITY_EDITOR
        // Draw helper gizmos in Scene view (for easier placement)
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