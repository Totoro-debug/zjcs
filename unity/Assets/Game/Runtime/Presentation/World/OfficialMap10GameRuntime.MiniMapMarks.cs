using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMap10GameRuntime
    {
        private sealed class NativeMiniMapMark
        {
            public GameObject Root;
            public OfficialMap11MiniMapBinding Binding;
            public OfficialMap11UiComponent[] Supports;
            public Transform[] ScaleContents;
            public Vector3[] OriginalScales;
        }
        private readonly Dictionary<int, NativeMiniMapMark> nativeMiniMapMarks = new Dictionary<int, NativeMiniMapMark>();

        // MiniMapMarkManager.CreateMark / MiniMapEntityMark.GetEntityMarkPos.
        private void RefreshNativeMiniMapMarks()
        {
            if (miniMapUiData?.minimapMarkBindings == null || minimapCamera == null) return;
            foreach (var binding in miniMapUiData.minimapMarkBindings)
            {
                var entity = mapEntities[binding.placementId];
                GameObject worldObject = entityView.FindEntityObject(entity.PlacementId);
                int state = puzzles != null && puzzles.Contains(entity.PlacementId) ? puzzles.State(entity.PlacementId) : entity.State;
                if (mapLogicTags.TryGetValue(entity.PlacementId, out var tag))
                    state = localSave.mapLogicStates.FirstOrDefault(value => value.Tag == tag.Tag)?.State ?? state;
                if (entity.TypeId == 202 && IsGoddessActivated(entity)) state = binding.completeState;
                bool completed = state == binding.completeState || localSave.completedEntityPlacements.Contains(entity.PlacementId);
                bool active = worldObject != null && worldObject.activeSelf && IsRegionAccessible(entity.Region)
                    && !(binding.hideInEnd && completed);
                if (!nativeMiniMapMarks.TryGetValue(entity.PlacementId, out var mark))
                {
                    if (!active) continue;
                    var root = Instantiate(Resources.Load<GameObject>("Map11/UI/" + binding.key), minimapCamera.transform.parent, false);
                    root.name = binding.key + "_" + binding.placementId;
                    Vector3 position = GridToWorld(entity.Grid);
                    root.transform.localPosition = new Vector3(position.x + binding.offset.x, position.y + binding.offset.y, 0);
                    SetLayerRecursively(root, 30);
                    var supports = miniMapUiData.components.Where(value => value.panel == binding.key
                        && value.type != "NativeAnimation").ToArray();
                    mark = new NativeMiniMapMark { Root = root, Binding = binding, Supports = supports };
                    Transform Find(string path) => MiniMapMarkObject(mark, path);
                    mark.ScaleContents = supports.Where(value => value.type == "MinimapMarkKeepSizeObjSupport")
                        .Select(value => Find(value.fields.mMyContent)).ToArray();
                    mark.OriginalScales = mark.ScaleContents.Select(value => value.localScale).ToArray();
                    nativeMiniMapMarks.Add(entity.PlacementId, mark);
                }
                bool selected = !miniMapFilters.TryGetValue(binding.key, out bool filter) || filter;
                mark.Root.SetActive(active && selected && (fullMiniMapPanel == null || fullMiniMapClassId == mapClassId));
                foreach (var support in mark.Supports)
                {
                    if (support.type == "MiniMapActiveSwitcherSupport") ApplyMiniMapMarkState(mark, support.fields, completed ? 1 : 0);
                    else if (support.type == "MiniMapActiveByNPCVisibleSupporter") ApplyMiniMapMarkState(mark, support.fields, active ? 1 : 0);
                    else if (support.type == "MapEntityTipDotNodeLinker") ApplyMiniMapMarkState(mark, support.fields, 0);
                }
            }
            RefreshNativeMiniMapMarkScale();
        }

        private Transform MiniMapMarkObject(NativeMiniMapMark mark, string path)
        {
            string prefix = "/" + mark.Binding.key;
            return path == prefix ? mark.Root.transform : mark.Root.transform.Find(path.Substring(prefix.Length + 1));
        }

        private void ApplyMiniMapMarkState(NativeMiniMapMark mark, OfficialMap11UiComponentFields fields, int state)
        {
            foreach (var reference in fields.references?.RefIds ?? Array.Empty<OfficialMap11ManagedReference>())
            {
                var rows = reference.data.mStateGameObjectData?.GoConfig;
                if (rows == null) continue;
                var enabled = rows.Where(value => value.State.mValue == state).SelectMany(value => value.GameObjects).ToArray();
                foreach (string path in rows.SelectMany(value => value.GameObjects).Distinct())
                    MiniMapMarkObject(mark, path).gameObject.SetActive(enabled.Contains(path));
            }
        }

        // MiniMapKeepSizeHandler.GetScale / MinimapKeepSizeObjHelper.Zoom.
        private void RefreshNativeMiniMapMarkScale()
        {
            foreach (var mark in nativeMiniMapMarks.Values)
            {
                int index = 0;
                foreach (var support in mark.Supports.Where(value => value.type == "MinimapMarkKeepSizeObjSupport"))
                {
                    Vector3 scale = mark.OriginalScales[index];
                    if (fullMiniMapCamera != null)
                    {
                        var camera = FullMiniMapSettings.CameraControllerSetting;
                        float rate = (fullMiniMapCamera.orthographicSize - camera.MinCameraOrthographicSize)
                            / (camera.MaxCameraOrthographicSize - camera.MinCameraOrthographicSize);
                        float threshold = FullMiniMapSettings.MarkZoomThreshold > 0
                            ? FullMiniMapSettings.MarkZoomThreshold : support.fields.mDefaultZoomThreshold;
                        if (threshold > 0 && rate > 0 && rate <= 1)
                        {
                            float size = Mathf.Lerp(camera.MinCameraOrthographicSize, camera.MaxCameraOrthographicSize, threshold);
                            scale = Vector3.one * (rate <= threshold ? 1 : fullMiniMapCamera.orthographicSize / size);
                        }
                    }
                    mark.ScaleContents[index++].localScale = scale;
                }
            }
        }
    }
}
