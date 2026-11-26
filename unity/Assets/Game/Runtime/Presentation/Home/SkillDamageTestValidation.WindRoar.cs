using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.BattleStatusPlayables;
using Zjcs.Official.EntityActions;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Home
{
    public sealed partial class SkillDamageTestValidation
    {
        private static readonly Dictionary<IFightStatusPlayableState, Action<IFightStatusPlayableState>> windRoarObservers =
            new Dictionary<IFightStatusPlayableState, Action<IFightStatusPlayableState>>();
        private static WindRoarReceipt windRoar;
        private static Vector3[] windRoarPose;

        private static void PrepareAcquiredWindRoar()
        {
            ReleaseWindRoarObservers();
            windRoar = new WindRoarReceipt(); windRoarPose = null;
        }

        private static void VerifyAcquiredWindRoar(FightBattleSkillCastEvent cast,
            FightBattleDamageEvent[] damage, FightBattleCheckpoint checkpoint, string output)
        {
            float[] delays = { .77f, .83f, .97f, 1.21f, 1.43f };
            Require(cast.HitSelections.Count == 5 && cast.HitSelections.All(hit => hit.HitEntityId == 11131)
                && cast.HitSelections.Select(hit => hit.Delay).Zip(delays, (a, b) => Math.Abs(a - b) < .0001f).All(value => value),
                "The acquired wind roar lost its five original hit references or timing.");
            Require(damage.Length >= 5 && damage.All(hit => hit.SourceId == 1 && hit.TargetId != 1
                && hit.SkillItemId == 10057 && hit.HitEntityId == 11131 && hit.DamageEntityId == 11132
                && hit.ElementType == 100 && hit.DamageType == 1 && !hit.TrueDamage && !hit.IsCure),
                "The acquired wind roar lost its normal wind damage or original hit graph.");
            windRoar.casts++; windRoar.hits += damage.Length;
            string state = FightBattleCheckpointSerializer.Save(checkpoint);
            var restored = FightBattleCheckpointSerializer.Load(state);
            var runner = OfficialCombatAssetCatalog.LoadDefault().RestoreBattle(restored);
            Require(runner.Step(checkpoint.Sequence, checkpoint.Counter).Replayed
                && FightBattleCheckpointSerializer.Save(restored) == state,
                "Restoring wind roar repeated its damage or airborne inputs.");
            File.WriteAllText(Path.Combine(output, "acquired-wind-roar-checkpoint.json"), state);
            File.WriteAllText(Path.Combine(output, "acquired-wind-roar-strikes.json"), JsonUtility.ToJson(cast, true));
        }

        private static bool ObserveAcquiredWindRoarPhase(int statusId)
        {
            foreach (var status in FindObjectsOfType<MyFightStatus>())
            {
                var entity = status.GetComponent<MyEntity>();
                if (!entity || entity.Entity == null || entity.Entity.ClassId != statusId || !status.Context?.Owner) continue;
                var owner = status.Context.Owner;
                var bodyEntity = owner.GetComponent<MyEntity>();
                // The fixture monster's own 14005 also launches the player. Only
                // its body receives the acquired Wind Roar being verified here.
                if (!bodyEntity || bodyEntity.Id.EntityId != 2) continue;
                Require(CurrentStatusViewLookup.Get(status) != null,
                    "Wind roar selected an older airborne catalog instead of its declared current source.");
                var renderer = owner.GetComponentInChildren<BattleNativeMonsterRenderer>();
                var meshRenderer = renderer ? renderer.GetComponent<MeshRenderer>() : null;
                var mesh = renderer ? renderer.GetComponent<MeshFilter>()?.sharedMesh : null;
                if (!renderer || !renderer.Animator || !meshRenderer || !meshRenderer.enabled
                    || meshRenderer.forceRenderingOff || !meshRenderer.isVisible || !mesh || mesh.vertexCount == 0) continue;
                var vertices = mesh.vertices;
                Require(vertices.All(value => FiniteWindRoar(value.x) && FiniteWindRoar(value.y) && FiniteWindRoar(value.z)),
                    "The original wind roar airborne model contains invalid geometry.");
                if (statusId == 24269)
                {
                    var driver = status.GetComponentInChildren<ECViewDriver>(true);
                    var source = driver ? driver.GetView<ECStatusInvokedEventSourceView>() : null;
                    Require(source != null, "The current airborne status lost its original Invoke event source.");
                    var playables = new HashSet<GameObject>(); source.TryGetPlayables(ref playables);
                    var host = (IFightViewHandlerHost)source;
                    var lookup = host.ServiceProvider.GetService(typeof(IMyActionEntityLookup)) as IMyActionEntityLookup;
                    var manager = host.ServiceProvider.GetService(typeof(IMyPlayableManager)) as IMyPlayableManager;
                    Require(playables.Count == 1 && playables.Single().name == "playable_AirCombo"
                        && bodyEntity && lookup?.GetEntity(bodyEntity.Id) == bodyEntity && manager != null,
                        "The original airborne handler lost its nested playable or actual owner/manager binding.");
                    if (!windRoarObservers.ContainsKey(status.Component))
                    {
                        Action<IFightStatusPlayableState> observer = _ => windRoar.invokes++;
                        windRoarObservers.Add(status.Component, observer);
                        status.Component.OnInvoked += observer;
                    }
                    if (renderer.Animation != "floating_01") continue;
                    if (windRoarPose == null) windRoarPose = vertices;
                    else windRoar.geometryAdvanced |= vertices.Length != windRoarPose.Length
                        || vertices.Where((value, index) => (value - windRoarPose[index]).sqrMagnitude > 1e-10f).Any();
                }
                return true;
            }
            return false;
        }

        private static bool FiniteWindRoar(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static void CompleteAcquiredWindRoar(string output)
        {
            ReleaseWindRoarObservers();
            Require(windRoar.casts > 0 && windRoar.hits >= 5 && windRoar.invokes > 0
                && windRoar.launchCaptured && windRoar.recoveryCaptured && windRoar.geometryAdvanced,
                "The acquired wind roar did not render, animate, invoke and finish both original airborne phases.");
            File.WriteAllText(Path.Combine(output, "acquired-wind-roar-receipt.json"), JsonUtility.ToJson(windRoar, true));
            Debug.Log("[Acquired active combat] 10057 five wind hits, current airborne event chain, native animation and restore passed.");
        }

        private static void ReleaseWindRoarObservers()
        {
            foreach (var observer in windRoarObservers) observer.Key.OnInvoked -= observer.Value;
            windRoarObservers.Clear();
        }

        [Serializable]
        private sealed class WindRoarReceipt
        {
            public int casts, hits, invokes;
            public bool launchCaptured, recoveryCaptured, geometryAdvanced;
        }
    }
}
