using System;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Home;
using Zjcs.Presentation.Combat;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Editor
{
    public static partial class HomeBattleHostPlayModeAcceptance
    {
        [Serializable] private sealed class SceneBindings { public SceneUnit[] units; }
        [Serializable] private sealed class SceneUnit { public int entityId; public SceneModel model; }
        [Serializable] private sealed class SceneModel { public SceneTransform[] transforms; }
        [Serializable] private sealed class SceneTransform { public string name; public float[] position, scale; }

        private static Transform BattlePlayerEntity(MonoBehaviour runner)
        {
            var controller = runner.GetComponent<OfficialBattleRuntimeController>();
            var entity = controller.TryGetUnitTransform(1);
            Require(entity != null, "The real controller has no shared player entity.");
            return entity;
        }

        private static void VerifyAttachedPlayerPresentation(MonoBehaviour runner, FightBattleCheckpoint checkpoint)
        {
            Transform entity = BattlePlayerEntity(runner);
            var home = runner.GetComponentInParent<HomeFeatureRuntime>();
            Transform player = entity.GetComponentInChildren<ProfessionNativeSceneRenderer>(true)?.transform;
            Require(entity.name == "3100" && player != null && player.GetComponent<Animator>() != null,
                "The shared battle scene did not create the original native player/Animator.");
            Require(!home.HomePlayerTransform.IsChildOf(entity) && !home.HomePlayerTransform.gameObject.activeInHierarchy,
                "The battle borrowed or exposed the suspended home avatar.");
            var data = JsonUtility.FromJson<SceneBindings>(Resources.Load<TextAsset>("Combat/battle-scene-bindings").text);
            SceneUnit binding = Array.Find(data.units, value => value.entityId == 3100);
            Transform model = entity.Find("3100_view");
            Require(model != null, "The native player lost its original 3100_view root.");
            foreach (var part in binding.model.transforms)
            {
                model = model.Find(part.name);
                Require(model != null, "The player lost an official battle model transform: " + part.name);
                Require(Vector3.Distance(model.localPosition, new Vector3(part.position[0], part.position[1],
                        part.position[2])) < .001f
                    && Vector3.Distance(new Vector3(Mathf.Abs(model.localScale.x), Mathf.Abs(model.localScale.y),
                        Mathf.Abs(model.localScale.z)), new Vector3(part.scale[0], part.scale[1], part.scale[2])) < .001f,
                    "The player's official battle offset/scale was multiplied by the host model transform.");
            }
            Require(player == model, "The battle player is not the original embedded Game model.");
            Transform hud = entity.Find("OfficialBattleUnitHud_1");
            // A completed death releases its HUD. Keep requiring the original
            // anchor while a live player or a retained death HUD is present.
            var state = checkpoint?.Units?.Find(unit => unit.Id == 1);
            Require(state != null, "The presentation fixture lost its actual player checkpoint.");
            if (state.Hp <= 0 && hud == null) return;
            Require(hud != null && hud.parent == entity && hud.localPosition.sqrMagnitude < .000001f,
                "The player's health bar no longer shares the moving battle entity origin.");
            var camera = runner.GetComponentInParent<HomeFeatureRuntime>().Scene.Camera;
            Require(Mathf.Abs(camera.WorldToScreenPoint(player.position).x
                - camera.WorldToScreenPoint(hud.position).x) < .5f,
                "The player's model and health bar are horizontally misaligned in the rendered frame.");
        }
    }
}
