// Central index of all attachment assets; provides filtered queries by slot type and compatibility.

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GunAssemblyTool
{
    [CreateAssetMenu(fileName = "AttachmentRegistry", menuName = "GunAssemblyTool/Attachment Registry")]
    public class AttachmentRegistry : ScriptableObject
    {
        // Master list of every attachment asset in the project.
        // Populated by clicking "Auto Collect" in the Inspector (AttachmentRegistryEditor).
        public List<AttachmentData> allAttachments = new List<AttachmentData>();

        // Cache that groups attachments by slot type for fast filtered queries.
        // Rebuilt by RebuildIndex() on load and after every Inspector modification.
        private Dictionary<AttachmentType, List<AttachmentData>> _byType;

        // Called by Unity when the asset is loaded into memory.
        // Strips null entries left by deleted assets then rebuilds the index.
        private void OnEnable() => Refresh();

        // Called by Unity whenever the asset is modified in the Inspector.
        // Strips null entries then rebuilds the type index.
        private void OnValidate() => Refresh();

        // Removes null references from the list (caused by deleted assets)
        // then calls RebuildIndex. Prevents the Inspector from crashing
        // when Unity tries to serialise a null object reference.
        private void Refresh()
        {
            allAttachments = allAttachments.Where(a => a != null).ToList();
            RebuildIndex();
        }

        // Builds the _byType dictionary by grouping allAttachments on attachType.
        // Used internally; call Refresh() to trigger a full refresh including null removal.
        private void RebuildIndex()
        {
            _byType = new Dictionary<AttachmentType, List<AttachmentData>>();
            foreach (var a in allAttachments)
            {
                if (a == null) continue;
                if (!_byType.ContainsKey(a.attachType))
                    _byType[a.attachType] = new List<AttachmentData>();
                _byType[a.attachType].Add(a);
            }
        }

        // Returns every attachment of the given type that passes the full
        // compatibility check (tag rules + physical interface) against body.
        // Called by GunGenerator when selecting random attachments,
        // and by GunAssemblyController.GetCompatible for UI slot lists.
        public List<AttachmentData> GetCompatible(GunBodyData body, AttachmentType type)
        {
            if (body == null) return new List<AttachmentData>();
            if (_byType == null) RebuildIndex();
            if (!_byType.TryGetValue(type, out var list)) return new List<AttachmentData>();
            return list.Where(a => CompatibilityResolver.IsCompatible(body, a)).ToList();
        }

        // Returns every attachment of the given type with no compatibility filtering.
        // Useful for UI "show all" modes where incompatible items are greyed out
        // rather than hidden.
        public List<AttachmentData> GetAll(AttachmentType type)
        {
            if (_byType == null) RebuildIndex();
            return _byType.TryGetValue(type, out var list)
                ? new List<AttachmentData>(list)
                : new List<AttachmentData>();
        }

        // Finds and returns the attachment whose attachmentId matches the given string.
        // Called by GunAssemblyState.FromConfiguration when restoring a saved config
        // from a GunConfiguration snapshot.
        public AttachmentData FindById(string id)
        {
            return allAttachments.FirstOrDefault(a => a != null && a.attachmentId == id);
        }
    }
}
