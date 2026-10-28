using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityRadar;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Offline.World.NativeMap14SharedEntities;
using Zjcs.Offline.World.NativeMap14SharedViews;
namespace Zjcs.Offline.World.NativeMap14EntityRadar
{
    public sealed class NativeMap14RadarWorldQueries : IRadarWorldQueries
    {
        private readonly NativeMap14SharedAssemblies shared;
        private readonly NativeMap14SharedConfiguration configuration;
        private readonly object ground;
        private readonly Func<bool> initialized;
        private readonly Func<object, NativeMap14SharedViewEntity> wrap;
        public NativeMap14RadarWorldQueries(NativeMap14SharedAssemblies shared, NativeMap14SharedConfiguration configuration,
            object ground, Func<bool> initialized, Func<object, NativeMap14SharedViewEntity> wrap)
        {
            this.shared = shared ?? throw new ArgumentNullException(nameof(shared));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.ground = ground ?? throw new ArgumentNullException(nameof(ground));
            this.initialized = initialized ?? throw new ArgumentNullException(nameof(initialized));
            this.wrap = wrap ?? throw new ArgumentNullException(nameof(wrap));
            if (ground.GetType() != shared.CommonType("GridGroundComponent")) throw new ArgumentException("Original grid ground is required.");
        }
        public int MapStageCostShowDistance => (int)Get(configuration.Settings, "MapStageCostShowDistance");
        public IRadarGamePosData FindGamePosData(Pos2d position)
        {
            if (!initialized()) return null;
            var result = ground.GetType().GetMethod("GetPosData", new[] { shared.CommonType("Pos2d") })
                .Invoke(ground, new[] { Activator.CreateInstance(shared.CommonType("Pos2d"), position.X, position.Y) });
            return result == null ? null : new PositionData(result, wrap);
        }
        public static void BindBody(NativeMap14SharedViewEntity entity)
        {
            if (entity.Grid == null) return;
            if (entity.GetComponent<IRadarBodyInfo>() != null) return;
            entity.BindAdditionalComponent<IRadarBodyInfo>(new BodyInfo(Get(entity.Grid.Original, "Info")));
        }
        private static object Get(object owner, string name) => NativeMap14SharedPrefabs.Get(owner, name);
        private sealed class PositionData : IRadarGamePosData
        {
            private readonly object original;
            private readonly Func<object, NativeMap14SharedViewEntity> wrap;
            public PositionData(object original, Func<object, NativeMap14SharedViewEntity> wrap) { this.original = original; this.wrap = wrap; }
            public bool ExistEntitys() => (bool)NativeMap14SharedPrefabs.Call(original, "ExistEntitys");
            public IEnumerable<ECViewEntityData> Entitys
            { get { foreach (var entity in (IEnumerable)Get(original, "Entitys")) yield return wrap(entity); } }
        }
        private sealed class BodyInfo : IRadarBodyInfo
        {
            private readonly object original;
            public BodyInfo(object original) { this.original = original; }
            public Vector2 RealCenter => (Vector2)Get(original, "RealCenter");
            public IEnumerable<RadarBodyPosInfo> BodyRangeInfos
            {
                get
                {
                    foreach (var body in (IEnumerable)Get(original, "BodyRangeInfos"))
                    {
                        var position = Get(body, "Pos");
                        yield return new RadarBodyPosInfo { Pos = new Pos2d((int)Get(position, "X"), (int)Get(position, "Y")) };
                    }
                }
            }
        }
    }
}
