#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GunAssemblyTool.Editor
{
    /// <summary>
    /// Prefab 导入工具。
    /// 扫描用户枪身 Prefab 上的 GunAttachmentPoint 组件，
    /// 自动生成对应的 GunBodyData ScriptableObject 资产。
    /// </summary>
    public static class PrefabImporter
    {
        /// <summary>
        /// 扫描 Prefab 上所有 GunAttachmentPoint，返回挂载点信息列表（纯读取，不修改任何资产）。
        /// </summary>
        public static List<AttachmentPointInfo> ScanPrefab(GameObject prefab)
        {
            if (prefab == null) return new List<AttachmentPointInfo>();

            return prefab
                .GetComponentsInChildren<GunAttachmentPoint>(includeInactive: true)
                .Select(p => new AttachmentPointInfo
                {
                    slotType  = p.slotType,
                    localPath = GetTransformPath(prefab.transform, p.transform)
                })
                .ToList();
        }

        /// <summary>
        /// 根据 Prefab 扫描结果生成 GunBodyData 资产并保存。
        /// 生成后需要在 Inspector 中手动补充 Tags。
        /// </summary>
        /// <param name="prefab">枪身 Prefab。</param>
        /// <param name="savePath">保存路径，例如 "Assets/Data/Guns/MyGun_Body.asset"。</param>
        public static GunBodyData CreateBodyDataFromPrefab(GameObject prefab, string savePath)
        {
            if (prefab == null)
            {
                Debug.LogError("[PrefabImporter] Prefab 为空。");
                return null;
            }

            var points = ScanPrefab(prefab);
            if (points.Count == 0)
            {
                Debug.LogWarning(
                    $"[PrefabImporter] '{prefab.name}' 上未找到 GunAttachmentPoint 组件。\n" +
                    "请在 Prefab 的配件挂载位置添加 GunAttachmentPoint 组件后重试。");
            }

            var bodyData = ScriptableObject.CreateInstance<GunBodyData>();
            bodyData.bodyId        = prefab.name.ToLowerInvariant().Replace(" ", "_");
            bodyData.availableSlots = points.Select(p => p.slotType).Distinct().ToList();

            AssetDatabase.CreateAsset(bodyData, savePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[PrefabImporter] GunBodyData 已创建：{savePath}\n" +
                $"识别到的槽位：{string.Join(", ", bodyData.availableSlots)}\n" +
                "⚠ 请在 Inspector 中为该 Body 补充适配 Tags。");

            return bodyData;
        }

        /// <summary>
        /// 向 Prefab 实例添加挂载点组件并写回 Prefab 资产。
        /// 用于辅助用户在没有挂载点的 Prefab 上手动标注位置。
        /// </summary>
        public static GunAttachmentPoint AddAttachmentPoint(
            GameObject prefabInstance,
            AttachmentType slotType,
            Vector3 localPosition)
        {
            var go = new GameObject($"AttachPoint_{slotType}");
            go.transform.SetParent(prefabInstance.transform, worldPositionStays: false);
            go.transform.localPosition = localPosition;

            var point = go.AddComponent<GunAttachmentPoint>();
            point.slotType = slotType;

            var prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(prefabInstance);
            if (!string.IsNullOrEmpty(prefabPath))
            {
                PrefabUtility.SaveAsPrefabAssetAndConnect(
                    prefabInstance, prefabPath, InteractionMode.AutomatedAction);
                Debug.Log($"[PrefabImporter] 已向 '{prefabPath}' 添加 {slotType} 挂载点。");
            }

            return point;
        }

        private static string GetTransformPath(Transform root, Transform target)
        {
            var parts   = new List<string>();
            var current = target;
            while (current != null && current != root)
            {
                parts.Insert(0, current.name);
                current = current.parent;
            }
            return string.Join("/", parts);
        }
    }

    public class AttachmentPointInfo
    {
        public AttachmentType slotType;
        public string         localPath;
    }
}
#endif
