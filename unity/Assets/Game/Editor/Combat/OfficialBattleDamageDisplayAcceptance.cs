using System;
using System.Linq;
using UnityEditor.Build;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.Combat
{
    public static class OfficialBattleDamageDisplayAcceptance
    {
        public static void Run()
        {
            var root = new GameObject("DamageDisplayFixture");
            var hud = new GameObject("DamageDisplayHud", typeof(RectTransform));
            OfficialBattleSceneView scene = null;
            try
            {
                var catalog = OfficialBattleHudCatalog.LoadDefault();
                scene = new OfficialBattleSceneView(OfficialBattleModelCatalog.LoadDefault(), root.transform,
                    null, new OfficialBattleGridMapper(root.transform, new FightBattleGrid { Width = 4, Height = 4 }),
                    null, (transform, unit) => { }, catalog, hud.transform);
                var checkpoint = new FightBattleCheckpoint();
                checkpoint.Units.Add(new FightBattleUnitState { Id = 2, X = 1, Y = 1, Hp = 30, MaxHp = 100 });
                var step = new FightBattleStepEvent();
                step.DamageEvents.Add(new FightBattleDamageEvent
                    { TargetId = 2, HasDamageValue = true, DamageValue = 0, Damage = 0, ShieldDamage = 100 });
                step.DamageEvents.Add(new FightBattleDamageEvent
                    { TargetId = 2, HasDamageValue = true, DamageValue = -100, Damage = -30 });
                step.DamageEvents.Add(new FightBattleDamageEvent
                    { TargetId = 2, IsCure = true, HasDamageValue = true, DamageValue = 100, Damage = 10 });
                step.DamageEvents.Add(new FightBattleDamageEvent
                    { TargetId = 2, IsCure = true, HasDamageValue = true, DamageValue = 100, Damage = 0 });
                step.DamageEvents.Add(new FightBattleDamageEvent { TargetId = 2, Damage = -30, ShieldDamage = 70 });
                step.DamageEvents.Add(new FightBattleDamageEvent { TargetId = 2, Damage = -100, Critical = true });
                step.DamageEvents.Add(new FightBattleDamageEvent { TargetId = 2, Damage = -100, Block = true });
                step.DamageEvents.Add(new FightBattleDamageEvent { TargetId = 2, Damage = -100, ElementType = 200 });
                step.DamageEvents.Add(new FightBattleDamageEvent { TargetId = 2, Damage = -100, ElementType = 200, Critical = true });
                step.DamageEvents.Add(new FightBattleDamageEvent { TargetId = 2, Damage = -100, ElementType = 200, Block = true });
                scene.ShowDamageFloats(step, checkpoint, OfficialBattleDamageColors.Load());
                var floats = root.GetComponentsInChildren<OfficialBattleDamageFloat>(true);
                Require(floats.Length == 10 && floats.Select(value => value.EffectType).Distinct().Count() == 7,
                    "The configured seven numeric effect types were not restored.");
                foreach (var value in floats)
                    Require(value.GetComponentsInChildren<TextMesh>(true).Any(label => label.text == "100"
                            && label.font != null && label.font.material.mainTexture != null),
                        "Original number binding failed: " + value.name + ", labels="
                        + string.Join(";", value.GetComponentsInChildren<TextMesh>(true)
                            .Select(label => label.name + "=" + label.text)));
                VerifyNumberPixels(root);
                Debug.Log("Original damage display passed: seven configured world effects and ten original bitmap numbers retain shield-only, overkill, overheal and legacy values of 100.");
            }
            finally
            {
                scene?.Dispose();
                UnityEngine.Object.DestroyImmediate(hud);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void VerifyNumberPixels(GameObject root)
        {
            var cameraObject = new GameObject("OriginalNumberRasterCamera");
            var target = new RenderTexture(512,512,24);
            var pixels = new Texture2D(512,512,TextureFormat.RGBA32,false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                foreach (var node in root.GetComponentsInChildren<Transform>(true)) node.gameObject.layer=31;
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled=false;
                camera.orthographic=true;
                camera.orthographicSize=3;
                camera.clearFlags=CameraClearFlags.SolidColor;
                camera.backgroundColor=Color.black;
                camera.cullingMask=1<<31;
                camera.transform.position=new Vector3(1,2,-10);
                camera.targetTexture=target;
                camera.Render();
                RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,512,512),0,0);
                pixels.Apply();
                int visible=pixels.GetPixels32().Count(value => value.r>8 || value.g>8 || value.b>8);
                string output=Environment.GetEnvironmentVariable("ZJCS_BATTLE_NUMBERS_PREVIEW_OUTPUT");
                if (!string.IsNullOrEmpty(output)) System.IO.File.WriteAllBytes(output,pixels.EncodeToPNG());
                Require(visible>20,"Original bitmap font produced no visible pixels: "+visible);
                Debug.Log("Original bitmap font raster passed: "+visible+" visible pixels.");
            }
            finally
            {
                RenderTexture.active=previous;
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(pixels);
            }
        }

        public sealed class Probe
        {
            private int counter, targetId;
            private bool verified;
            public string Observation { get; private set; } = "No display observation.";

            public bool TryVerify(FightBattleCheckpoint checkpoint)
            {
                if (verified) return true;
                if (checkpoint.Counter < counter) return false;
                var hit = checkpoint.Receipts.Single(value => value.Counter == counter).Event.DamageEvents
                    .First(value => value.SourceId == 1 && value.TargetId == targetId && value.ShieldDamage > 0);
                Require(hit.HasDamageValue && hit.DamageValue == 0 && hit.Damage == 0
                    && hit.BeforeHp == hit.AfterHp, "The live shield-only fixture lost raw or actual HP values.");
                string number = OfficialBattleSceneView.FormatBattleNumber(-hit.ShieldAddFinalDamageValue);
                Observation = "counter=" + checkpoint.Counter + ", first=" + counter + ", delay=" + hit.Delay
                    + ", wanted=" + number + ", floats=" + string.Join(";", UnityEngine.Object.FindObjectsOfType<OfficialBattleDamageFloat>(true)
                        .Select(value => value.name + ":" + value.TargetId + ":" + value.gameObject.activeInHierarchy
                            + ":" + string.Join(",", value.GetComponentsInChildren<TextMesh>(true)
                                .Select(label => label.text + "/a=" + label.color.a + "/bounds="
                                    + label.GetComponent<Renderer>().bounds.size))));
                var numberText = UnityEngine.Object.FindObjectsOfType<OfficialBattleDamageFloat>()
                    .Where(value => value.TargetId == targetId).SelectMany(value => value.GetComponentsInChildren<TextMesh>())
                    .FirstOrDefault(label => label.text == number && label.color.a > 0.05f
                        && label.GetComponent<Renderer>().isVisible && label.GetComponent<Renderer>().bounds.size.sqrMagnitude > 0f);
                if (numberText == null) return false;
                VerifyCameraPixels(numberText.GetComponent<Renderer>());
                verified = true;
                Debug.Log("Damage-display Play Mode passed: shield-only hit displayed " + number
                    + ", actual HP loss=0, absorbed=" + hit.ShieldDamage + ", counter=" + counter + ".");
                return true;
            }

            private static void VerifyCameraPixels(Renderer number)
            {
                Camera camera=Camera.main;
                Require(camera!=null,"The live damage number has no battle camera.");
                var target=new RenderTexture(Screen.width,Screen.height,24);
                var image=new Texture2D(Screen.width,Screen.height,TextureFormat.RGBA32,false);
                RenderTexture previousActive=RenderTexture.active, previousTarget=camera.targetTexture;
                float previousAspect=camera.aspect;
                bool enabled=number.enabled;
                try
                {
                    camera.targetTexture=target;
                    camera.aspect=(float)Screen.width/Screen.height;
                    camera.Render();
                    RenderTexture.active=target;
                    image.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);
                    image.Apply();
                    Color32[] shown=image.GetPixels32();
                    number.enabled=false;
                    camera.Render();
                    image.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);
                    image.Apply();
                    Color32[] hidden=image.GetPixels32();
                    int changed=0;
                    for(int i=0;i<shown.Length;i++)
                        if(Math.Abs(shown[i].r-hidden[i].r)+Math.Abs(shown[i].g-hidden[i].g)+Math.Abs(shown[i].b-hidden[i].b)>8) changed++;
                    Require(changed>3,"The number contributes no actual battle-camera pixels: "+changed
                        +", layer="+number.gameObject.layer+", mask="+camera.cullingMask
                        +", screen="+camera.WorldToScreenPoint(number.bounds.center)
                        +", material="+number.sharedMaterial.name+", shader="+number.sharedMaterial.shader.name);
                    Debug.Log("Original live damage number raster passed: "+changed+" actual battle-camera pixels.");
                }
                finally
                {
                    number.enabled=enabled;
                    camera.targetTexture=previousTarget;
                    camera.aspect=previousAspect;
                    RenderTexture.active=previousActive;
                    UnityEngine.Object.DestroyImmediate(target);
                    UnityEngine.Object.DestroyImmediate(image);
                }
            }

            public static Probe Install(OfficialBattleRuntimeController controller)
            {
                var assets = OfficialCombatAssetCatalog.LoadDefault();
                var checkpoint = controller.Checkpoint;
                var player = checkpoint.Units.Single(value => value.Id == 1);
                var target = checkpoint.Units.Single(value => value.MonsterId > 0);
                target.Hp = target.MaxHp = target.Turn.Hp = 20000;
                int maxHp = assets.LoadDamageFormula().PropertyId("MaxHp");
                foreach (var properties in new[] { target.Properties, target.BaseProperties })
                    if (properties != null)
                        foreach (var property in properties.Where(value => value.Id == maxHp)) property.Value = 20000;
                foreach (var skill in player.Skills) { skill.HasCustomDamage = true; skill.CustomDamage = 100; }
                var control = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
                var runner = assets.RestoreBattle(control);
                int firstCounter = 0;
                for (int i = 0; i < 32 && !control.Finished; i++)
                {
                    var step = runner.Step(control.Sequence, control.Counter + 1);
                    if (!step.Event.DamageEvents.Any(hit => hit.SourceId == 1 && hit.TargetId == target.Id && hit.Damage < 0)) continue;
                    firstCounter = control.Counter;
                    break;
                }
                Require(firstCounter > 0, "The live shield fixture has no player damage.");
                target.Statuses.Add(new FightBattleStatusState
                {
                    ClassId = 99998, InstanceId = ++checkpoint.StatusInstanceSerial, DurationRound = -1,
                    ShieldKind = FightBattleShieldKind.Persistent, ShieldValue = 5000,
                    ShieldRemainValue = 5000, ShieldInitialized = true
                });
                return new Probe { counter = firstCounter, targetId = target.Id };
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new BuildFailedException(message);
        }
    }
}
