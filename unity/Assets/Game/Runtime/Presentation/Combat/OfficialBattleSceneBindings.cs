using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Rendering;
using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Combat
{
    /// <summary>Recovered prefab renderer layers, expressed in the existing flattened scene order.</summary>
    internal static class OfficialBattleSceneBindings
    {
        [Serializable] private sealed class Data { public string manifest; public Unit[] units; public CameraBinding[] cameras; }
        [Serializable] private sealed class Receipt { public string manifest, output_sha256; }
        [Serializable] internal sealed class RendererBinding
        {
            public string name;
            public int sortingLayer, sortingLayerId, sortingOrder;
            public TransformBinding[] transforms;
            public int Order => checked(sortingLayer * 1000 + sortingOrder);
        }
        [Serializable] internal sealed class TransformBinding
        {
            public string name;
            public float[] position, rotation, scale;
        }
        [Serializable] internal sealed class Unit
        {
            public int entityId;
            public RendererBinding model;
            public RendererBinding[] bars;
        }
        [Serializable] internal sealed class CameraBinding
        {
            public int gameId;
            public string sceneName;
            public float[] position;
            public float orthographicSize;
        }
        private static Dictionary<int, CameraBinding> cameras;
        private static Dictionary<int, Unit> units;

        public static Unit Find(int entityId)
        {
            if (units == null)
            {
                TextAsset payload = Resources.Load<TextAsset>("Combat/battle-scene-bindings");
                TextAsset source = Resources.Load<TextAsset>("Combat/battle-scene-bindings-source-manifest");
                if (payload == null || source == null)
                    throw new InvalidOperationException("Official battle scene bindings are missing.");
                Receipt receipt = JsonUtility.FromJson<Receipt>(source.text);
                string hash;
                using (SHA256 sha = SHA256.Create())
                    hash = BitConverter.ToString(sha.ComputeHash(payload.bytes)).Replace("-", "").ToLowerInvariant();
                Data data = JsonUtility.FromJson<Data>(payload.text);
                if (receipt == null || receipt.manifest != "854_156383"
                    || receipt.output_sha256 != hash || data == null
                    || data.manifest != receipt.manifest || data.units == null)
                    throw new InvalidOperationException("Official battle scene binding hashes differ.");
                var resolved = new Dictionary<int, Unit>();
                foreach (Unit unit in data.units)
                {
                    if (unit == null || unit.entityId <= 0 || unit.model == null)
                        throw new InvalidOperationException("Official battle model sorting binding is invalid.");
                    resolved.Add(unit.entityId, unit);
                }
                var resolvedCameras = new Dictionary<int, CameraBinding>();
                foreach (CameraBinding camera in data.cameras)
                {
                    if (camera == null || camera.gameId <= 0 || camera.position == null
                        || camera.position.Length != 3 || camera.orthographicSize <= 0)
                        throw new InvalidOperationException("Official battle camera binding is invalid.");
                    resolvedCameras.Add(camera.gameId, camera);
                }
                cameras = resolvedCameras;
                units = resolved;
            }
            return units.TryGetValue(entityId, out Unit result) ? result : null;
        }

        public static CameraBinding Camera(int gameId)
        {
            Find(0);
            return cameras.TryGetValue(gameId, out CameraBinding result) ? result : null;
        }

        public static int EntityId(FightBattleUnitState state)
        {
            if (state == null) return 0;
            if (state.MonsterTemplateId > 0 && Find(state.MonsterTemplateId) != null)
                return state.MonsterTemplateId;
            return state.MonsterId > 0 ? state.MonsterId : state.CharacterType == 1 ? 3100 : 0;
        }

        public static OfficialBattleUnitSorting BindModel(Transform body, int entityId)
        {
            Unit unit = Find(entityId);
            return unit == null ? null : new OfficialBattleUnitSorting(body, unit.model.Order);
        }
    }

    /// <summary>Keeps each native mesh's internal draw order and restores a reused map avatar.</summary>
    internal sealed class OfficialBattleUnitSorting : IDisposable
    {
        private readonly SortingGroup group;
        private readonly bool added, wasEnabled;
        private readonly int layer, order;
        public OfficialBattleUnitSorting(Transform body, int battleOrder)
        {
            group = body.GetComponent<SortingGroup>();
            added = group == null;
            if (added) group = body.gameObject.AddComponent<SortingGroup>();
            wasEnabled = group.enabled;
            layer = group.sortingLayerID;
            order = group.sortingOrder;
            group.enabled = true;
            group.sortingLayerID = 0;
            group.sortingOrder = battleOrder;
        }
        public void Dispose()
        {
            if (group == null) return;
            if (added) UnityEngine.Object.Destroy(group);
            else
            {
                group.sortingLayerID = layer;
                group.sortingOrder = order;
                group.enabled = wasEnabled;
            }
        }
    }
}
