using System;
using UnityEngine;
using Zjcs.Presentation.Home;

namespace Zjcs.Editor
{
    public static partial class HomeBattleHostPlayModeAcceptance
    {
        [Serializable] private sealed class SceneBindings { public SceneUnit[] units; }
        [Serializable] private sealed class SceneUnit { public int entityId; public SceneModel model; }
        [Serializable] private sealed class SceneModel { public SceneTransform[] transforms; }
        [Serializable] private sealed class SceneTransform { public string name; public float[] position, scale; }

        private static void VerifyAttachedPlayerPresentation(MonoBehaviour runner)
        {
            Transform player = runner.GetComponentInParent<HomeFeatureRuntime>().HomePlayerTransform;
            Transform entity = player;
            while (entity != null && entity.name != "Battle attached unit 3100") entity = entity.parent;
            Require(entity != null, "The player was not attached to the common battle entity hierarchy.");
            var data = JsonUtility.FromJson<SceneBindings>(Resources.Load<TextAsset>("Combat/battle-scene-bindings").text);
            SceneUnit binding = Array.Find(data.units, value => value.entityId == 3100);
            Transform model = entity;
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
            Require(player.parent == model && player.localPosition.sqrMagnitude < .000001f
                && Vector3.Distance(player.localScale, Vector3.one) < .001f,
                "The attached player retained a second map offset/scale.");
            Transform hud = entity.Find("OfficialBattleUnitHud_1");
            Require(hud != null && hud.parent == entity && hud.localPosition.sqrMagnitude < .000001f,
                "The player's health bar no longer shares the moving battle entity origin.");
            var camera = runner.GetComponentInParent<HomeFeatureRuntime>().Scene.Camera;
            Require(Mathf.Abs(camera.WorldToScreenPoint(player.position).x
                - camera.WorldToScreenPoint(hud.position).x) < .5f,
                "The player's model and health bar are horizontally misaligned in the rendered frame.");
        }
    }
}
