using System;
using System.Collections.Generic;
using System.IO;
using Spine.Unity;
using UnityEngine;

namespace Zjcs.Presentation.Opening
{
    /// <summary>MyFightActionSkillMove/MyPlayableTrack/MyCurveTrack and timed animator events.</summary>
    public sealed class OpeningBattleActionPlayer : MonoBehaviour
    {
        private sealed class Move
        {
            public int Owner, Index;
            public Transform Actor;
            public OpeningRecordEntity Entity;
            public Vector3 Start;
            public Vector3[] Path;
            public float Started, SegmentDuration, TrackDuration, CurveDuration, Speed;
            public AnimationCurve X, Y;
            public bool Flip;
        }
        private sealed class AnimationEvent
        {
            public int Owner, UnitId, ClassId;
            public SkeletonAnimation Unit;
            public string Clip, Idle;
            public float Start, Speed;
            public bool Loop;
        }
        private readonly Dictionary<int, Dictionary<string, object>> configs = new Dictionary<int, Dictionary<string, object>>();
        private readonly List<Move> moves = new List<Move>();
        private readonly List<AnimationEvent> animations = new List<AnimationEvent>();
        private readonly List<Dictionary<string, object>> nativeAnimations = new List<Dictionary<string, object>>();
        public int CompletedSegments { get; private set; }
        public readonly Dictionary<int, HashSet<string>> PlayedClips = new Dictionary<int, HashSet<string>>();
        public event Action<int, string> AnimationPlayed;

        public void Initialize()
        {
            var data = OpeningJson.Object(OpeningJson.Parse(File.ReadAllBytes(Path.Combine(
                Application.streamingAssetsPath, "Opening", "action-motion.json"))), "opening action motion");
            if (OpeningJson.String(data, "manifest") != OpeningAssets.Manifest)
                throw new FormatException("Opening movement configuration is not pinned.");
            foreach (object raw in OpeningJson.Array(data["motions"], "motions"))
            {
                var config = OpeningJson.Object(raw, "motion");
                configs.Add(OpeningJson.Int(config, "classId"), config);
            }
            foreach (object raw in OpeningJson.Array(data["animations"], "move skill animations"))
                nativeAnimations.Add(OpeningJson.Object(raw, "move skill animation"));
        }

        public IEnumerable<Dictionary<string, object>> GetEvents(int classId)
        {
            foreach (var entry in nativeAnimations)
                if (OpeningJson.Int(entry, "classId") == classId) yield return entry;
        }

        public void PlayAnimation(int owner, int unitId, int classId, SkeletonAnimation unit,
            string clip, string idle, float delay, float speed, bool loop = false)
        {
            animations.Add(new AnimationEvent { Owner = owner, UnitId = unitId, ClassId = classId,
                Unit = unit, Clip = clip, Idle = idle, Start = Time.time + delay / (speed * unit.AnimationState.TimeScale),
                Speed = speed, Loop = loop });
            TickAnimations();
        }

        public void MoveAlong(int owner, int classId, Transform actor, OpeningRecordEntity entity,
            Dictionary<string, object> component, float speed)
        {
            if (!configs.TryGetValue(classId, out var config))
                throw new InvalidOperationException("Official movement binding missing: " + classId);
            var points = OpeningJson.Array(component["Path"], "movement path");
            if (points.Count == 0) return;
            var path = new Vector3[points.Count];
            for (int i = 0; i < path.Length; i++)
            {
                var point = OpeningJson.Object(points[i], "movement point");
                path[i] = new Vector3(OpeningJson.Int(point, "X"), OpeningJson.Int(point, "Y"), 0);
            }
            var components = OpeningJson.Object(config["info"], "movement info");
            var info = OpeningJson.Object(components["FightActionSkillMoveComponent"], "move component info");
            var playable = OpeningJson.Object(config["playable"], "move playable");
            var curve = OpeningJson.Object(config["curve"], "move curve");
            // Common.FightActionSkillMoveComponent.StartImpl updates the logical final grid
            // before the client playable interpolates from the current visual position.
            if (OpeningJson.Bool(info, "UpdateFlipX"))
            {
                Vector3 previous = path.Length > 1 ? path[path.Length - 2] : actor.localPosition;
                float dx = path[path.Length - 1].x - previous.x;
                if (OpeningJson.Bool(info, "ReverseFlipX")) dx = -dx;
                if (dx != 0) entity.FlipX = dx < 0;
            }
            entity.GridX = (int)path[path.Length - 1].x;
            entity.GridY = (int)path[path.Length - 1].y;
            var move = new Move { Owner = owner, Actor = actor, Entity = entity, Path = path,
                SegmentDuration = (float)OpeningJson.Number(info, "MoveDuration"),
                Speed = speed, CurveDuration = (float)OpeningJson.Number(curve, "myDuration"),
                X = OpeningSpecialEffectRuntime.ReadCurve(curve, "myCurveX", 0),
                Y = OpeningSpecialEffectRuntime.ReadCurve(curve, "myCurveY", 0) };
            move.TrackDuration = move.CurveDuration / (float)OpeningJson.Number(playable, "myTrackSpeed", 1);
            BeginSegment(move);
            moves.Add(move);
        }

        private static void BeginSegment(Move move)
        {
            move.Started = Time.time;
            move.Start = move.Actor.localPosition;
            // MyFightActionSkillMove.DoMoveSourceTurn resets to the logical flip on the final segment.
            bool flip = move.Entity.FlipX;
            if (move.Index < move.Path.Length - 1)
            {
                float dx = move.Path[move.Index].x - move.Start.x;
                if (dx != 0) flip = dx < 0;
            }
            move.Actor.localScale = new Vector3(flip ? -1 : 1, 1, 1);
            move.Flip = flip ^ (move.Path[move.Index].y < move.Start.y);
        }

        public void StopOwner(int owner)
        {
            animations.RemoveAll(item => item.Owner == owner);
            for (int i = moves.Count - 1; i >= 0; i--)
            {
                Move move = moves[i];
                if (move.Owner != owner) continue;
                ResetGrid(move);
                moves.RemoveAt(i);
            }
        }

        private static void ResetGrid(Move move)
        {
            if (move.Actor == null) return;
            move.Actor.localPosition = new Vector3(move.Entity.GridX, move.Entity.GridY, 0);
            move.Actor.localScale = new Vector3(move.Entity.FlipX ? -1 : 1, 1, 1);
        }

        private void TickAnimations()
        {
            for (int i = 0; i < animations.Count;)
            {
                AnimationEvent item = animations[i];
                if (Time.time < item.Start) { i++; continue; }
                animations.RemoveAt(i);
                if (item.Unit == null) continue;
                var track = item.Unit.AnimationState.SetAnimation(0, item.Clip, item.Loop);
                track.TimeScale = item.Speed;
                if (!item.Loop && item.Idle != null)
                    item.Unit.AnimationState.AddAnimation(0, item.Idle, true, 0);
                if (!PlayedClips.TryGetValue(item.UnitId, out var clips))
                    PlayedClips.Add(item.UnitId, clips = new HashSet<string>());
                clips.Add(item.ClassId + ":" + item.Clip);
                AnimationPlayed?.Invoke(item.UnitId, item.Clip);
            }
        }

        private void Update()
        {
            TickAnimations();
            for (int i = moves.Count - 1; i >= 0; i--)
            {
                Move move = moves[i];
                if (move.Actor == null) { moves.RemoveAt(i); continue; }
                float elapsed = (Time.time - move.Started) * move.Speed;
                float t = move.TrackDuration > 0 ? Mathf.Clamp01(elapsed / move.TrackDuration) : 1;
                Vector3 delta = move.Path[move.Index] - move.Start;
                float distance = delta.magnitude;
                Quaternion rotation = distance > 0 ? Quaternion.FromToRotation(Vector3.up, delta) : Quaternion.identity;
                var point = new Vector3(move.X.Evaluate(t * move.CurveDuration) * distance * (move.Flip ? -1 : 1),
                    move.Y.Evaluate(t * move.CurveDuration) * distance, 0);
                move.Actor.localPosition = move.Start + rotation * point;
                if (elapsed < move.SegmentDuration) continue;
                move.Actor.localPosition = move.Path[move.Index];
                CompletedSegments++;
                move.Index++;
                if (move.Index < move.Path.Length) BeginSegment(move);
                else { ResetGrid(move); moves.RemoveAt(i); }
            }
        }
    }
}
