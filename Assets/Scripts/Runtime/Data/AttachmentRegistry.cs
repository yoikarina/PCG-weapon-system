using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GunAssemblyTool
{
    // Holds all attachments and provides ways to query them
    [CreateAssetMenu(fileName = "AttachmentRegistry", menuName = "GunAssemblyTool/Attachment Registry")]
    public class AttachmentRegistry : ScriptableObject
    {
        // All available attachments in the game
        public List<AttachmentData> allAttachments = new List<AttachmentData>();

        // Cached lookup: group attachments by type (e.g. all scopes together)
        private Dictionary<AttachmentType, List<AttachmentData>> _byType;

        private void OnEnable() => RebuildIndex();
        private void OnValidate() => RebuildIndex();

        // Build the dictionary for faster access at runtime
        private void RebuildIndex()
        {
            _byType = new Dictionary<AttachmentType, List<AttachmentData>>();

            foreach (var a in allAttachments)
            {
                if (a == null) continue;

                // Create list for this type if it doesn't exist yet
                if (!_byType.ContainsKey(a.attachType))
                    _byType[a.attachType] = new List<AttachmentData>();

                _byType[a.attachType].Add(a);
            }
        }

        /// <summary>
        /// Get all attachments of a given type that CAN be used on this gun body
        /// </summary>
        public List<AttachmentData> GetCompatible(GunBodyData body, AttachmentType type)
        {
            // Safety check
            if (body == null) return new List<AttachmentData>();

            if (_byType == null) RebuildIndex();

            // No attachments of this type
            if (!_byType.TryGetValue(type, out var list))
                return new List<AttachmentData>();

            // Filter by compatibility rules
            return list.Where(a => CompatibilityResolver.IsCompatible(body, a)).ToList();
        }

        /// <summary>
        /// Get all attachments of a given type (no filtering)
        /// </summary>
        public List<AttachmentData> GetAll(AttachmentType type)
        {
            if (_byType == null) RebuildIndex();

            return _byType.TryGetValue(type, out var list)
                ? new List<AttachmentData>(list) // return a copy (avoid modifying original list)
                : new List<AttachmentData>();
        }
    }
}