using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.Combat
{
    public sealed class OfficialBattleSummonModelAcceptance
    {
        private readonly int entityId;
        private FightBattleUnitState generated;
        private OfficialBattleSummonModelAcceptance(int id){entityId=id;}

        public static OfficialBattleSummonModelAcceptance Install(OfficialBattleRuntimeController controller)
        {
            bool doll=Environment.GetEnvironmentVariable("ZJCS_BATTLE_TEST_SUMMON_MODEL")=="4600";
            var test=new OfficialBattleSummonModelAcceptance(doll?4600:3181);
            var player=controller.Checkpoint.Units.First(u=>u.CharacterType==1);
            var oldSkills=player.Skills;var oldSlots=player.SkillState.Slots;
            var catalog=OfficialCombatAssetCatalog.LoadDefault().Load();
            var skill=catalog.CreateSkill(doll?12200:10710,0,doll?10097:10019,5,20,1,null);
            player.Skills=new List<FightBattleSkillDefinition>{skill};
            player.SkillState.Slots=new List<FightSkillSlotState>{catalog.CreateSkillSlot(skill,0)};
            controller.StepCompleted+=result=>
            {
                if(test.generated!=null)return;
                test.generated=result.Checkpoint.Units.FirstOrDefault(u=>u.MonsterId==test.entityId);
                if(test.generated==null)return;
                player.Skills=oldSkills;player.SkillState.Slots=oldSlots;
                Debug.Log("Original summon generated through battle execution: "+test.entityId+", unit="+test.generated.Id);
            };
            return test;
        }

        public IEnumerator Verify(OfficialBattleRuntimeController controller)
        {
            if(generated==null && entityId==4600)
            {
                var player=CreateFixtureUnit(1,1,1,100007,101);var enemy=CreateFixtureUnit(2,2,2,2000000,1);
                enemy.X=6;
                var checkpoint=new FightBattleCheckpoint {StageId=1001,RandomSeed=13,
                    ResultRules=new FightBattleResultRules()};
                checkpoint.Units.Add(player);checkpoint.Units.Add(enemy);
                var assets=OfficialCombatAssetCatalog.LoadDefault();var catalog=assets.Load();
                var skill=catalog.CreateSkill(12200,0,10097,5,20,1,null);
                player.Skills.Add(skill);player.SkillState.Slots.Add(catalog.CreateSkillSlot(skill,0));
                FightBattleExecutor.Begin(checkpoint,assets.LoadDamageFormula(),new FightDamageSettlement(),
                    catalog,assets.LoadSkillItems()).Step(0,1);
                generated=checkpoint.Units.FirstOrDefault(u=>u.VoodooDoll!=null);
                Require(generated!=null,"Official 12200 did not generate its 4600 substitute entity.");
                var battlePlayer=controller.Checkpoint.Units.First(u=>u.CharacterType==1);
                generated.X=battlePlayer.X;generated.Y=battlePlayer.Y+2;
                controller.Checkpoint.Units.Add(generated);
                var battleScene=(OfficialBattleSceneView)typeof(OfficialBattleRuntimeController).GetField("sceneView",
                    BindingFlags.Instance|BindingFlags.NonPublic).GetValue(controller);
                battleScene.Refresh(controller.Checkpoint);
            }
            Require(generated!=null,"Actual battle did not generate its original summon.");
            yield return new WaitForSecondsRealtime(.3f);
            var source=Resources.Load<OfficialSummonSkeletonData>("Combat/Summons/"+entityId+"-data");
            var unit=GameObject.Find("BattleUnit_"+generated.Id);
            Require(unit!=null && unit.GetComponentsInChildren<MeshFilter>().Any(m=>m.sharedMesh.vertexCount>0),
                "Actual summoned unit has no native geometry.");
            string output=Environment.GetEnvironmentVariable("ZJCS_BATTLE_INPUT_OUTPUT")??"../output/unity/battle-summon-model";
            Directory.CreateDirectory(output);
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"summoned-"+entityId+".png"));
            yield return null;
            foreach(var renderer in unit.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled).Take(3))
                Debug.Log("Summon native draw: "+renderer.name+", visible="+renderer.isVisible+", position="+renderer.transform.position+", bounds="+renderer.bounds
                    +", layer="+renderer.gameObject.layer+", queue="+renderer.sharedMaterial.renderQueue+", camera="+Camera.main.cullingMask
                    +", pipeline="+UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline);
            Require(unit.GetComponentsInChildren<MeshRenderer>().Any(r=>r.enabled && r.sharedMaterial==source.Material
                && GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(Camera.main),r.bounds)),
                "Actual summoned unit has no original material inside the camera: "+unit.transform.position);
            var card=GameObject.Find("GameRoundCharacterItem_"+generated.Id);
            Require((card!=null)==source.Source.showOnRoundProgress,"Original summon round display flag was ignored.");
            if(card!=null)Require(card.GetComponentsInChildren<SpriteRenderer>(true).Any(r=>r.sprite==source.Portrait)
                && source.Portrait.vertices.Length==9,"Original summon portrait geometry is not bound to its timeline.");
            controller.SetPaused(true);
            yield return null;
            yield return VerifyRaster(unit);
            // Exercise every original channel and constraint in the running renderer, including
            // negative draw-order offsets, attachment changes and clipping at intermediate times.
            var fixture=new GameObject("SummonNativePoseFixture");fixture.transform.position=unit.transform.position;
            unit.SetActive(false);
            using(var model=new OfficialSummonNativeModelView(source,fixture.transform,"Original weighted summon"))
            {
                foreach(var animation in source.Source.animations)
                {
                    for(int frame=0;frame<=60;frame++)
                    {
                        model.ApplyPose(animation,animation.duration*frame/60f);
                        VerifyIk(source.Source,model,animation,animation.duration*frame/60f);
                        foreach(var mesh in fixture.GetComponentsInChildren<MeshFilter>())
                            foreach(var vertex in mesh.sharedMesh.vertices)
                                Require(!float.IsNaN(vertex.x) && !float.IsInfinity(vertex.x) && !float.IsNaN(vertex.y)
                                    && !float.IsInfinity(vertex.y),"Original summon pose produced invalid geometry: "+animation.name);
                    }
                    model.ApplyPose(animation,animation.duration*.5f);
                    yield return null;
                    ScreenCapture.CaptureScreenshot(Path.Combine(output,"pose-"+entityId+"-"+animation.name+".png"));
                    yield return null;
                }
            }
            UnityEngine.Object.Destroy(fixture);unit.SetActive(true);
            var scene=(OfficialBattleSceneView)typeof(OfficialBattleRuntimeController).GetField("sceneView",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(controller);
            generated.Ended=true;scene.Refresh(controller.Checkpoint);yield return null;yield return null;
            Require(GameObject.Find("BattleUnit_"+generated.Id)==null,"Expired living summon retained its model or HUD.");
            generated.Ended=false;scene.Refresh(controller.Checkpoint);yield return null;
            Require(GameObject.Find("BattleUnit_"+generated.Id)!=null,"Restored living summon lost its native model.");
            controller.SetPaused(false);
            Debug.Log("Original summon native Play Mode passed: "+entityId+", actual battle spawn, original material visibility, all "
                +source.Source.animations.Length+" animations at 61 samples and runtime screenshots.");
        }
        private static IEnumerator VerifyRaster(GameObject unit)
        {
            var renderers=unit.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled).ToArray();
            var bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);
            Vector3 lo=Camera.main.WorldToScreenPoint(bounds.min),hi=Camera.main.WorldToScreenPoint(bounds.max);
            var target=new RenderTexture(Screen.width,Screen.height,24);
            var pixels=new Texture2D(Screen.width,Screen.height,TextureFormat.RGBA32,false);
            RenderTexture previous=RenderTexture.active;int changed=0;
            try
            {
                Color32[] Capture()
                {
                    UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(Camera.main,
                        new UnityEngine.Rendering.RenderPipeline.StandardRequest {destination=target});
                    RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0);pixels.Apply();
                    return pixels.GetPixels32();
                }
                var a=Capture();unit.SetActive(false);var b=Capture();unit.SetActive(true);
                for(int y=Mathf.Max(0,Mathf.FloorToInt(lo.y));y<Mathf.Min(pixels.height,Mathf.CeilToInt(hi.y));y++)
                    for(int x=Mathf.Max(0,Mathf.FloorToInt(lo.x));x<Mathf.Min(pixels.width,Mathf.CeilToInt(hi.x));x++)
                    {int i=y*pixels.width+x;if(Mathf.Abs(a[i].r-b[i].r)+Mathf.Abs(a[i].g-b[i].g)+Mathf.Abs(a[i].b-b[i].b)>8)changed++;}
            }
            finally
            {
                unit.SetActive(true);RenderTexture.active=previous;
                UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(pixels);
            }
            Require(changed>20,"Actual summoned native mesh did not change pixels inside its projected geometry bounds: "+changed);
            Debug.Log("Original summon native raster pixels: "+changed);
            yield return null;
        }
        private static void VerifyIk(OfficialSummonSkeletonSource source,OfficialSummonNativeModelView model,
            OfficialSummonAnimation animation,float time)
        {
            for(int i=0;i<source.constraints.Length;i++)
            {
                var c=source.constraints[i];if(c.child<0)continue;
                float mix=c.mix;
                foreach(var track in animation.tracks)
                    if(track.kind==3 && track.index==i && track.axis==0 && time>=track.keys[0].time)mix=track.Curve.Evaluate(time);
                if(mix<.99999f)continue;
                Transform parent=model.Bones[c.parent],child=model.Bones[c.child],target=model.Bones[c.target];
                float l1=Vector3.Distance(parent.position,child.position);
                float l2=child.TransformVector(Vector3.right*source.bones[c.child].length*source.scale).magnitude;
                float distance=Vector3.Distance(parent.position,target.position);
                if(distance>l1+l2+.0001f || distance<Mathf.Abs(l1-l2)-.0001f)continue;
                Vector3 end=child.TransformPoint(Vector3.right*source.bones[c.child].length*source.scale);
                Require(Vector3.Distance(end,target.position)<.002f,"Original summon IK failed to reach its target: "+animation.name+"/"+c.name);
            }
        }
        private static FightBattleUnitState CreateFixtureUnit(int id,int side,int type,long hp,int speed)
        {
            var unit=new FightBattleUnitState {Id=id,Side=side,CharacterType=type,Hp=hp,MaxHp=hp,
                LastMaxHp=hp,Level=150,SubRank=1,X=5,Y=5};
            unit.Properties.Add(new FightPropertyValue {Id=1,Value=10001});
            unit.Properties.Add(new FightPropertyValue {Id=2,Value=10003});
            unit.Properties.Add(new FightPropertyValue {Id=3,Value=hp});
            unit.Properties.Add(new FightPropertyValue {Id=4,Value=speed});
            unit.Turn=new FightTurnUnit {Id=id,Hp=hp,Speed=speed};return unit;
        }
        private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    }
}
