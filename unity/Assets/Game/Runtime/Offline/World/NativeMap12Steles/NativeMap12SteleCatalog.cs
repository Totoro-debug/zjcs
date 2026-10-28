using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Official.SteleEntities;

namespace Zjcs.Offline.World.NativeMap12Steles
{
    public sealed class NativeMap12SteleCatalog : ScriptableObject
    {
        [Serializable] public sealed class Display
        {
            public int entityClassId;
            public GameObject prefab;
            public Vector2 realCenter;
            public List<Pos2d> bodyRange;
        }
        [Serializable] public sealed class Placement
        { public int placementId, entityClassId, periodicityGroupId, periodicityMaxCount, x, y, direction; }
        public string manifest;
        public Display[] displays;
        public Placement[] placements;
        public Display FindDisplay(int entityClassId) => displays.SingleOrDefault(v => v.entityClassId == entityClassId);
        public Placement FindPlacement(int placementId) => placements.SingleOrDefault(v => v.placementId == placementId);
        public static NativeMap12SteleCatalog Load()
        {
            var value = Resources.Load<NativeMap12SteleCatalog>("Map12/Native/stele-entities-catalog");
            if (!value || value.manifest != "871_156598" || value.displays?.Length != 2
                || value.placements?.Length != 7
                || !value.displays.Select(v => v.entityClassId).SequenceEqual(new[] { 6650, 6655 })
                || value.placements.Select(v => v.placementId).Distinct().Count() != 7
                || value.displays.Any(v => !v.prefab || v.prefab.GetComponent<ECViewDriver>() == null || v.bodyRange == null)
                || value.placements.Any(v => value.FindDisplay(v.entityClassId) == null || v.periodicityMaxCount <= 0))
                throw new InvalidDataException("Original native Map12 stele entity catalog is incomplete.");
            return value;
        }
    }
}
