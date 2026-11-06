using System;
using System.Reflection;
using UnityEngine;
using Zjcs.Official.EntityClientComponents;
using Zjcs.Offline.World.NativeMap14SharedEntities;

namespace Zjcs.Offline.World.NativeMap14ClientComponents
{
    // Calls unchanged original shared methods, including the lazy parser-based
    // threshold. It never replaces elapsed time or movement formulae.
    public sealed class NativeMap14MovementRuntime : IMapEntityMovementRuntime
    {
        private readonly MainlineSharedWorld world;
        private readonly object clock;
        private readonly MethodInfo getTick, getDateTime, elapsed, within;
        private readonly PropertyInfo threshold;
        private readonly object[] dateArguments;

        public NativeMap14MovementRuntime(MainlineSharedWorld world)
        {
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            RequireActive();
            var time = world.Assemblies.CommonType("TimeManager");
            clock = time.GetProperty("Instance").GetValue(null);
            getTick = time.GetMethod("GetTick", new[] { typeof(DateTimeKind) });
            getDateTime = time.GetMethod("GetDateTime", new[] { typeof(DateTimeKind) });
            dateArguments = new[] { getDateTime?.GetParameters()[0].DefaultValue };
            elapsed = time.GetMethod("CalcElapsedSecond", new[] { typeof(long) });
            var utils = world.Assemblies.CommonType("MapMoveUtils");
            threshold = utils.GetProperty("SClientDistanceThreshold");
            within = utils.GetMethod("IsWithinThresholdSqrDistance", new[] { typeof(Vector2), typeof(Vector2) });
            if (clock == null || getTick == null || getDateTime == null || elapsed == null || threshold == null || within == null)
                throw new MissingMemberException("Original movement runtime methods are missing.");
        }

        private void RequireActive()
        {
            if (!world.IsReady || world.Assemblies == null)
                throw new InvalidOperationException("The original movement world must remain running.");
        }
        public long GetTick() { RequireActive(); return (long)getTick.Invoke(clock, dateArguments); }
        public DateTime GetDateTime() { RequireActive(); return (DateTime)getDateTime.Invoke(clock, dateArguments); }
        public double CalcElapsedSecond(long tick)
        { RequireActive(); return (double)elapsed.Invoke(clock, new object[] { tick }); }
        public float SClientDistanceThreshold
        { get { RequireActive(); return (float)threshold.GetValue(null); } }
        public bool IsWithinThresholdSqrDistance(Vector2 pos1, Vector2 pos2)
        { RequireActive(); return (bool)within.Invoke(null, new object[] { pos1, pos2 }); }
    }
}
