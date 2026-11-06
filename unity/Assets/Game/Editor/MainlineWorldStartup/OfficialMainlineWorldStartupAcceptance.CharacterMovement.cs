using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Official.EntityClientComponents;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Offline.World.NativeMap14ClientComponents;
using Zjcs.Offline.World.NativeMap14SharedEntities;
using Zjcs.Offline.World.NativeMap14SharedViews;

namespace Zjcs.Editor.MainlineWorldStartup
{
    public static partial class OfficialMainlineWorldStartupAcceptance
    {
        // Original agent/handler bodies over the actual shared character grid.
        // Only time is controlled; effects are observed through their real port.
        private sealed class CharacterMovementComponents : IDisposable
        {
            private readonly NativeMap14SharedViewCache views;
            private readonly NativeMap14MovementRuntime originalRuntime;
            private readonly CharacterMovementClock clock;
            private readonly GameObject host;
            private readonly MyEntity entity;
            private readonly MyGridTransform grid;
            private readonly MoveAgent agent;
            private readonly List<string> events = new List<string>();
            private NativeMap14SharedViewEntity previous;
            private bool running;

            public CharacterMovementComponents(MainlineSharedWorld world)
            {
                originalRuntime = new NativeMap14MovementRuntime(world);
                clock = new CharacterMovementClock(originalRuntime);
                views = new NativeMap14SharedViewCache(world.Assemblies);
                host = new GameObject("Original character movement fixture"); host.SetActive(false);
                entity = host.AddComponent<MyEntity>();
                host.AddComponent<MyTransform>();
                grid = host.AddComponent<MyGridTransform>();
                var effect = host.AddComponent<CharacterMovementEffect>(); effect.Events = events;
                agent = host.AddComponent<MoveAgent>(); agent.Runtime = clock;
                agent.OnStartMove += value =>
                {
                    Require(value == host && agent.IsMoving, "start callback owner/state differs"); events.Add("start");
                };
                agent.OnStopMove += value =>
                {
                    Require(value == host && !agent.IsMoving, "stop callback owner/state differs"); events.Add("stop");
                };
                host.SetActive(true);
                agent.enabled = false; // The same original frame body is driven below.
            }

            private static void Require(bool condition, string message)
            { if (!condition) throw new InvalidDataException("Original character movement: " + message); }

            private void Position(Vector3 expected, string message)
            { Require(Vector2.Distance(host.transform.localPosition, expected) < .0001f, message); }

            public void Verify(MainlineSharedWorld world)
            {
                var view = views.Wrap(world.ClientCharacter);
                Require(previous == null || ReferenceEquals(view, previous), "region transition replaced the retained grid view");
                Require(Math.Abs((originalRuntime.GetDateTime() - DateTime.UtcNow).TotalSeconds) < 5,
                    "original runtime did not read its local clock");
                Require(originalRuntime.GetTick() > 0 && originalRuntime.CalcElapsedSecond(originalRuntime.GetTick()) < 1,
                    "original elapsed-time query differs");
                var threshold = originalRuntime.SClientDistanceThreshold;
                Require(threshold >= 0 && originalRuntime.IsWithinThresholdSqrDistance(Vector2.zero, Vector2.zero)
                    && !originalRuntime.IsWithinThresholdSqrDistance(Vector2.zero, new Vector2(threshold + 1, 0)),
                    "original lazy parser threshold query differs");
                entity.Bind(null, view, previous == null);
                Require(ReferenceEquals(grid.Component, view.Grid) && ReferenceEquals(grid.Info, view.Grid.Info),
                    "original grid or Info binding differs");
                entity.MyStart(); running = true;
                try
                {
                    Require(view.Grid.ReceiverCount == 1, "original start did not subscribe to the grid");
                    var start = grid.Pos;
                    var next = new Pos2d(start.X + 1, start.Y);
                    var end = new Pos2d(start.X + 2, start.Y);
                    var extension = new Pos2d(start.X + 3, start.Y);
                    var path = new List<Pos2d> { start, next, end };
                    var p0 = grid.GridToPosition(start); var p1 = grid.GridToPosition(next);
                    var p2 = grid.GridToPosition(end); var p3 = grid.GridToPosition(extension);
                    events.Clear();
                    agent.Move((List<Pos2d>)null, 2); agent.Move(new List<Pos2d>(), 2);
                    Require(!agent.IsMoving && events.Count == 0, "empty path changed movement");
                    clock.Seconds = 0;
                    agent.Move(path, 2, clock.GetTick(), true, MapMoveType.Slide);
                    Require(events.SequenceEqual(new[] { "start", "effect:Slide" }), "start/effect ordering differs");
                    clock.Seconds = .25; agent.Advance(.25f);
                    Position(Vector3.Lerp(p0, p1, .5f), "clock-based path interpolation differs");
                    clock.Seconds = 1; agent.Advance(.75f); agent.Advance(0);
                    Position(p2, "original path did not finish at its endpoint");
                    Require(!agent.IsMoving && events.SequenceEqual(new[] { "start", "effect:Slide", "stop", "effect:stop" }),
                        "endpoint stop/effect order differs");
                    int notifications = events.Count; agent.Stop();
                    Require(events.Count == notifications, "redundant stop fired another notification");

                    grid.SetPositionByGrid(start); clock.Seconds = 2;
                    agent.Move(path, 2, clock.GetTick(), true);
                    agent.UpdatePath(1, new List<Pos2d> { next, end, extension });
                    clock.Seconds = 3.5; agent.Advance(2); agent.Advance(0);
                    Position(p3, "moving path append did not retain its prefix");
                    Require(!agent.IsMoving, "appended path did not stop");
                    notifications = events.Count;
                    agent.UpdatePath(-1, path);
                    Require(!agent.IsMoving && events.Count == notifications, "invalid stopped-path update restarted movement");
                    agent.UpdatePath(0, path);
                    Require(agent.IsMoving, "valid stopped-path repair did not restart");
                    agent.Stop(true, p1);
                    Position(p1, "forced stop did not set the requested position");
                    Require(!agent.IsMoving, "forced stop did not finish");

                    grid.SetPositionByGrid(start); clock.Seconds = 4;
                    agent.Move(path, 2, clock.GetTick(), true);
                    var far = p0 + new Vector3(threshold + 2, 0, 0);
                    agent.Stop(false, far);
                    Position(far, "original far-distance stop correction differs");
                    Require(!agent.IsMoving, "corrected stop did not finish");
                    agent.Move(new List<Vector3> { p0, p1 }, 1);
                    int transfers = 0; Pos2d transferred = default;
                    Action<Pos2d> transfer = value => { transfers++; transferred = value; };
                    agent.OnTransferEnd += transfer;
                    agent.DoTransferByAction(new CharacterMovementTransfer(end));
                    agent.OnTransferEnd -= transfer;
                    Require(transfers == 1 && transferred == end, "action transfer did not publish its original target");

                    grid.SetPositionByGrid(start); clock.Seconds = 5;
                    agent.Move(path, 2, clock.GetTick(), true);
                    entity.End(null); running = false;
                    Require(agent.IsMoving && view.Grid.ReceiverCount == 0, "component end changed the separate agent remove contract");
                    entity.Remove(null);
                    Position(p0, "original removal did not force-stop at the logical grid");
                    Require(!agent.IsMoving, "original removal retained movement");
                    entity.OnGameObjectPoolReset();
                    previous = view;
                    Debug.Log($"MAINLINE_CHARACTER_MOVEMENT_PASS map={world.Data.MapClassId} interpolation=True path_update=True original_threshold=True stop_transfer_remove=True retained=True");
                }
                finally
                {
                    if (running) { running = false; entity.End(null); entity.Remove(null); entity.OnGameObjectPoolReset(); }
                }
            }

            public void Dispose()
            {
                if (running) { running = false; entity.End(null); entity.Remove(null); }
                UnityEngine.Object.DestroyImmediate(host);
                views.Dispose();
            }
        }

        private sealed class CharacterMovementClock : IMapEntityMovementRuntime
        {
            private readonly NativeMap14MovementRuntime original;
            private readonly long epoch;
            public double Seconds;
            public CharacterMovementClock(NativeMap14MovementRuntime original)
            { this.original = original; epoch = original.GetTick(); }
            public long GetTick() => epoch + (long)(Seconds * TimeSpan.TicksPerSecond);
            public DateTime GetDateTime() => new DateTime(GetTick(), DateTimeKind.Utc);
            public double CalcElapsedSecond(long tick) => Math.Max(0, (GetTick() - tick) / (double)TimeSpan.TicksPerSecond);
            public float SClientDistanceThreshold => original.SClientDistanceThreshold;
            public bool IsWithinThresholdSqrDistance(Vector2 pos1, Vector2 pos2) => original.IsWithinThresholdSqrDistance(pos1, pos2);
        }

        private sealed class CharacterMovementTransfer : IActionMovementProvider
        {
            private readonly Pos2d target;
            public CharacterMovementTransfer(Pos2d target) { this.target = target; }
            public Pos2d GetTargetPos() => target;
        }

        private sealed class CharacterMovementEffect : MonoBehaviour, IMoveEffectSupport
        {
            public List<string> Events;
            public void PlayEffect(MapMoveType type) => Events.Add("effect:" + type);
            public void StopEffect() => Events.Add("effect:stop");
        }
    }
}
