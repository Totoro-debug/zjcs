using System;
using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.BattleNativePlayables;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.StoryPlayables;
using Zjcs.Presentation.Combat;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.Combat
{
    public static partial class OfficialBattleAnimatorPlaybackAcceptance
    {
        public static void RunVisibility()
        {
            RunCasts(); SessionState.SetBool(Key + ".partVisibility", true);
        }

        private static IEnumerator CheckPartVisibility(GameObject actor, Func<int, float, IEnumerator> pump, Action<string> capture)
        {
            var root = new GameObject("Original role part visibility host validation");
            var staging = new GameObject("Inactive original visibility leaf");
            staging.transform.SetParent(root.transform, false); staging.SetActive(false);
            var state = new FightBattleCheckpoint();
            state.Units.Add(new FightBattleUnitState { Id = 1, CharacterType = 1, Hp = 100, MaxHp = 100 });
            state.Units.Add(new FightBattleUnitState { Id = 2, CharacterType = 1, MonsterTemplateId = 3318, Hp = 100, MaxHp = 100, X = 2 });
            state.Units.Add(new FightBattleUnitState { Id = 3, CharacterType = 2, MonsterId = 3201, MonsterTemplateId = 3201, Hp = 100, MaxHp = 100, X = 4 });
            var other = new GameObject("UI-only original role binding fixture");
            var empty = new GameObject("Empty original part list fixture");
            other.transform.SetParent(root.transform, false); empty.transform.SetParent(root.transform, false);
            var mapper = new OfficialBattleGridMapper(root.transform, new FightBattleGrid { Width = 8, Height = 8 });
            using var scene = new OfficialBattleSceneView(OfficialBattleModelCatalog.LoadDefault(), root.transform,
                _ => null, mapper, (_, __) => null, (_, __) => { }, OfficialBattleHudCatalog.LoadDefault(), root.transform);
            using var scope = new OfflineBattleNativePlayableScope(root.transform, null);
            using var playback = new OfficialBattleSkillGraphPlayback(OfficialBattleSkillGraphCatalog.TryLoadDefault(), scope,
                new OfficialBattleSkillEntityPlayback(root.transform, mapper, scope, _ => null));
            var previousParent = actor.transform.parent;
            try
            {
                scene.Refresh(state);
                scene.AttachUnitTransform(1, actor.transform); scene.AttachUnitTransform(2, other.transform);
                scene.AttachUnitTransform(3, empty.transform);
                Transform Entity(Transform model)
                {
                    while (model.parent != root.transform && model.parent != null) model = model.parent;
                    Require(model.parent == root.transform, "production attached entity root"); return model;
                }
                var entity = Entity(actor.transform); var otherEntity = Entity(other.transform); var emptyEntity = Entity(empty.transform);
                Transform Hud(Transform entityRoot) => entityRoot.GetComponentsInChildren<Canvas>(true)
                    .Single(canvas => canvas.name.StartsWith("OfficialBattleUnitHud_", StringComparison.Ordinal)).transform;
                var hud = Hud(entity); var otherHud = Hud(otherEntity); var emptyHud = Hud(emptyEntity);
                var body = entity.Find("body"); var shadow = entity.Find("3100_view/ShadowBoneFollower/Shadow");
                Require(body && shadow && hud && otherHud && emptyHud, "actual production body, HUD and original shadow bindings");
                var host = (IBattleCharacterPartVisibilityHost)scene;
                host.SetCharacterPartVisibility(null, false); host.SetCharacterPartVisibility(actor, false);
                Require(hud.gameObject.activeSelf && shadow.gameObject.activeSelf, "null and non-entity sources leave current role references unchanged");
                host.SetCharacterPartVisibility(otherEntity.gameObject, false);
                Require(!otherHud.gameObject.activeSelf && other.gameObject.activeInHierarchy && hud.gameObject.activeSelf,
                    "original 3318 list changes only its UI and leaves its body and other roles visible");
                host.SetCharacterPartVisibility(otherEntity.gameObject, true);
                host.SetCharacterPartVisibility(emptyEntity.gameObject, false);
                Require(emptyHud.gameObject.activeSelf && empty.gameObject.activeInHierarchy, "original empty list does not hide any role part");

                var prefab = Resources.Load<GameObject>("Combat/SkillGraphs/Prefabs/EcEntity/Fight/FightSkill/13120/playable13120");
                var original = prefab.GetComponentsInChildren<MyPlayableCharacterPartVisibility>(true).Single();
                Require(!original.mStartVisible && original.mEndVisible && original.mDuration == .65f,
                    "original 13120 visibility leaf retains serialized start/end/duration fields");
                var template = Object.Instantiate(original.gameObject, staging.transform); template.SetActive(false);
                playback.BindSceneHosts(null, visibilityHost: scene);
                IEnumerator Advance(int frames)
                {
                    for (int i = 0; i < frames; i++)
                    {
                        yield return pump(1, .01f); scope.Tick(.01f); scope.LateTick(); scene.Tick(.01f);
                    }
                }
                void CheckHidden()
                {
                    Require(!hud.gameObject.activeSelf && !shadow.gameObject.activeSelf && body.gameObject.activeSelf
                        && actor.gameObject.activeInHierarchy && otherHud.gameObject.activeSelf,
                        "production host applies only the player's two original references while native body remains visible");
                }
                void CheckRestored() => Require(hud.gameObject.activeSelf && shadow.gameObject.activeSelf
                    && body.gameObject.activeSelf, "original stop fields restore configured references");
                var graph = scope.Play(template, entity.gameObject, otherEntity.gameObject);
                CheckHidden(); capture("parts-hidden");
                yield return Advance(70); Require(graph.myStatus == MyPlayable.PlayableStatus.Stop, "original duration ends visibility leaf");
                CheckRestored(); capture("parts-restored");
                var reused = scope.Play(template, entity.gameObject, otherEntity.gameObject);
                Require(ReferenceEquals(graph, reused), "original visibility leaf is reused from scope pool"); CheckHidden();
                scope.Stop(reused, true); yield return Advance(2); CheckRestored();
                scope.Play(template, entity.gameObject, otherEntity.gameObject); CheckHidden();
                scope.Dispose(); CheckRestored();
                Object.Destroy(hud.gameObject); yield return null;
                host.SetCharacterPartVisibility(entity.gameObject, false);
                Require(!shadow.gameObject.activeSelf && body.gameObject.activeSelf,
                    "destroyed serialized UI reference is skipped while remaining original references still change");
                host.SetCharacterPartVisibility(entity.gameObject, true);
                scene.DetachUnitTransform(1); yield return null;
                Require(actor.transform.parent == previousParent, "scene teardown retains borrowed native role");
                Debug.Log("[Original part visibility] actual shared scene/native player HUD and shadow, UI-only and empty source lists, original 13120 leaf fields, production binding, duration, interruption, pooled reuse and scope disposal passed. Full 13120 graph and monster live shadow ports are separate.");
            }
            finally
            {
                playback.Dispose(); scene.Dispose(); Object.DestroyImmediate(other); Object.DestroyImmediate(empty); Object.DestroyImmediate(root);
            }
        }
    }
}
