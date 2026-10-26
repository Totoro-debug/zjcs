using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.Combat
{
    public static class OfficialBattleWorldTipsAcceptance
    {
        public static void Run()
        {
            bool previousAsync=ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation=false;
            var root=new GameObject("OriginalWorldTipFixture");
            var hud=new GameObject("OriginalWorldTipHud",typeof(RectTransform));
            hud.transform.SetParent(root.transform,false);
            OfficialBattleSceneView scene=null;
            try
            {
                scene=new OfficialBattleSceneView(OfficialBattleModelCatalog.LoadDefault(),root.transform,null,
                    new OfficialBattleGridMapper(root.transform,new FightBattleGrid {Width=4,Height=4}),
                    null,(tr,unit)=>{},OfficialBattleHudCatalog.LoadDefault(),hud.transform);
                var checkpoint=new FightBattleCheckpoint();
                checkpoint.Units.Add(new FightBattleUnitState {Id=2,X=1,Y=1,Hp=100,MaxHp=100});
                int[] types={4,8,9,10,12};string[] words={"免疫","回避","未命中","重置冷却","驱散"};
                for(int i=0;i<types.Length;i++)
                {
                    foreach(var old in root.GetComponentsInChildren<OfficialBattleDamageFloat>(true))
                        UnityEngine.Object.DestroyImmediate(old.gameObject);
                    var step=new FightBattleStepEvent();
                    var hit=new FightBattleDamageEvent {TargetId=2,ElementType=101,
                        Immunity=types[i]==4,Dodge=types[i]==8,Blinding=types[i]==9};
                    if (types[i]==10) step.SkillCasts.Add(new FightBattleSkillCastEvent {ActorId=2,SkillEntityId=13220,IsReplay=true});
                    else if (types[i]==12) step.DispersedUnitIds.Add(2);
                    else step.DamageEvents.Add(hit);
                    scene.ShowDamageFloats(step,checkpoint,OfficialBattleDamageColors.Load());
                    var view=root.GetComponentsInChildren<OfficialBattleDamageFloat>(true).Single();
                    Require(view.EffectType==types[i] && view.name.EndsWith("_Scene",StringComparison.Ordinal),"The native world tip prefab was not selected.");
                    var text=view.GetComponentsInChildren<TextMeshPro>(true).Single();
                    Require(text.text==words[i] && text.font.name.Contains("FZY4K")
                        && Mathf.Approximately(text.fontSize,36) && text.fontStyle==FontStyles.Normal
                        && Mathf.Approximately(view.StackHeight,types[i]==10?.226f:.268f),"Original world tip text/font/height binding differs.");
                    Require(text.enableVertexGradient==(types[i]==10),"Original world tip gradient enable flag differs.");
                    Require(text.fontSharedMaterial.shader.name=="Zjcs/Combat/WorldTextMeshPro"
                        && text.fontMaterial.IsKeywordEnabled("OUTLINE_ON")
                        && Mathf.Approximately(text.fontMaterial.GetFloat("_OutlineWidth"),1f),"Original tip shader or outline differs.");
                    scene.Tick(types[i]==10?.35f:.12f);
                    text.ForceMeshUpdate();
                    Require(text.color.a>.05f && text.GetComponent<Renderer>().bounds.size.sqrMagnitude>0,
                        "Original world tip animation or dynamic glyphs did not become visible: alpha="+text.color.a
                        +", bounds="+text.GetComponent<Renderer>().bounds.size+", active="+text.gameObject.activeInHierarchy
                        +", chars="+(text.textInfo==null?-1:text.textInfo.characterCount)+", glyphs="+(text.font.characterLookupTable==null?-1:text.font.characterLookupTable.Count)
                        +", mesh="+(text.mesh==null?"null":text.mesh.vertexCount.ToString())+", scale="+text.transform.lossyScale);
                    if (types[i]<10)
                    {
                        Color expected=OfficialBattleDamageColors.Load().Resolve(hit,101).linear;
                        Require(Mathf.Abs(text.color.r-expected.r)<.001f && Mathf.Abs(text.color.g-expected.g)<.001f
                            && Mathf.Abs(text.color.b-expected.b)<.001f,"Tip RGB no longer follows original damage color selection.");
                    }
                    if (types[i]==10) VerifyReplayNative(view,text);
                    VerifyPixels(view,text,types[i]);
                }
                foreach(var old in root.GetComponentsInChildren<OfficialBattleDamageFloat>(true))UnityEngine.Object.DestroyImmediate(old.gameObject);
                var hiddenStep=new FightBattleStepEvent();
                int hiddenId=OfficialBattleWorldTips.Load().replaySkills.First(v=>!v.show).entity;
                hiddenStep.SkillCasts.Add(new FightBattleSkillCastEvent {ActorId=2,SkillEntityId=hiddenId,IsReplay=true});
                hiddenStep.SkillCasts.Add(new FightBattleSkillCastEvent {ActorId=2,SkillEntityId=13220,IsReplay=true,ReplayType=1});
                scene.ShowDamageFloats(hiddenStep,checkpoint,OfficialBattleDamageColors.Load());
                Require(root.GetComponentsInChildren<OfficialBattleDamageFloat>(true).Length==0,"A disabled replay or Extra replay produced a tip.");
                hiddenStep.SkillCasts.Clear();
                hiddenStep.SkillCasts.Add(new FightBattleSkillCastEvent {ActorId=2,SkillEntityId=13220,IsReplay=true,ReplayType=2});
                scene.ShowDamageFloats(hiddenStep,checkpoint,OfficialBattleDamageColors.Load());
                Require(root.GetComponentsInChildren<OfficialBattleDamageFloat>(true).Single().EffectType==10,"ExtraWithTip replay lost its original tip.");
                Debug.Log("Original world tips passed: five native prefabs, original words/font/material/outline, animation, damage color, replay flags and actual raster pixels.");
            }
            finally
            {
                scene?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
                ShaderUtil.allowAsyncCompilation=previousAsync;
            }
        }

        private static void VerifyReplayNative(OfficialBattleDamageFloat view,TextMeshPro text)
        {
            Require(Mathf.Abs(text.colorGradient.topLeft.r-.96862745f)<.0001f
                && Mathf.Abs(text.colorGradient.bottomLeft.r-.5411765f)<.0001f,"Original replay gradient changed.");
            var sprites=view.GetComponentsInChildren<SpriteRenderer>(true);
            Require(sprites.Length==3 && sprites.Count(v=>v.drawMode==SpriteDrawMode.Sliced)==1
                && sprites.All(v=>v.sharedMaterial.shader.name=="Zjcs/Combat/WorldSpriteUnlit"),"Original replay backgrounds/arrow/materials differ.");
            var binding=OfficialBattleWorldTips.Find("damage_FightResetSkillCDFloatEffect");
            Require(binding.prefab.nodes.Any(v=>v.animations.Any(a=>a.name=="GameCanvas_CharacterUI_Delay_Scene"
                && a.tracks.Length==13)),"Original replay legacy animation is incomplete.");
        }

        private static void VerifyPixels(OfficialBattleDamageFloat view,TMP_Text text,int type)
        {
            var cameraObject=new GameObject("WorldTipRasterCamera");
            var target=new RenderTexture(512,512,24);
            var pixels=new Texture2D(512,512,TextureFormat.RGBA32,false);
            RenderTexture previous=RenderTexture.active;
            try
            {
                foreach(var tr in view.GetComponentsInChildren<Transform>(true))tr.gameObject.layer=31;
                var camera=cameraObject.AddComponent<Camera>();
                camera.enabled=false;camera.orthographic=true;camera.orthographicSize=2;
                camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
                camera.transform.position=text.transform.position+new Vector3(0,0,-10);camera.targetTexture=target;
                camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,512,512),0,0);pixels.Apply();
                int count=pixels.GetPixels32().Count(v=>v.r>8 || v.g>8 || v.b>8);
                Require(count>20,"Original world tip produced no visible pixels: "+type+"="+count);
                string output=Environment.GetEnvironmentVariable("ZJCS_BATTLE_WORLD_TIPS_PREVIEW");
                if(!string.IsNullOrEmpty(output))System.IO.File.WriteAllBytes(System.IO.Path.Combine(output,"tip-"+type+".png"),pixels.EncodeToPNG());
                Debug.Log("Original world tip raster: type="+type+", pixels="+count+", PlayMode="+Application.isPlaying+".");
            }
            finally
            {
                RenderTexture.active=previous;
                UnityEngine.Object.DestroyImmediate(cameraObject);UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(pixels);
            }
        }

        public sealed class Probe
        {
            private int counter,targetId,type;
            private string word;
            private bool verified;
            public string Observation {get;private set;}

            public static Probe Install(OfficialBattleRuntimeController controller)
            {
                Run(); // All five native visuals also render inside Play Mode with controlled result flags.
                string mode=Environment.GetEnvironmentVariable("ZJCS_BATTLE_TEST_WORLD_TIP_MODE");
                bool blind=mode=="blind",replay=mode=="replay",disperse=mode=="disperse";
                var checkpoint=controller.Checkpoint;
                var assets=OfficialCombatAssetCatalog.LoadDefault();var formula=assets.LoadDamageFormula();
                var player=checkpoint.Units.Single(v=>v.Id==1);
                if (replay || disperse)
                {
                    var rules=assets.Load();
                    var target=checkpoint.Units.First(v=>v.MonsterId>0);
                    player.X=target.X-1;player.Y=target.Y;
                    player.Skills[0]=rules.CreateSkill(replay?13220:12940,0,player.Skills[0].ItemId,1,1,3,player.Properties);
                    if (replay)
                    {
                        var state=rules.CreateStatus(24031,player.Id,player.Id,++checkpoint.StatusInstanceSerial);
                        state.StackCount=10;player.Statuses.Add(state);
                    }
                    else
                    {
                        player.Statuses.Add(rules.CreateStatus(24012,player.Id,player.Id,++checkpoint.StatusInstanceSerial));
                        var skill=player.Skills[0];
                        var hit=FightBattleHitCollector.CollectAll(skill).First(v=>v.DisperseStatusTypes.Count>0);
                        skill.HitGraphRoots.Clear();skill.HitGraphRootEntityIds.Clear();skill.HitGraphRootEntityId=0;
                        skill.Hits.Clear();skill.Hits.Add(hit);
                    }
                }
                Action<FightBattleUnitState,string,long> set=(unit,name,value)=>
                {
                    int id=formula.PropertyId(name);Require(id>0,"Missing original property: "+name);
                    foreach(var list in new[] {unit.Properties,unit.BaseProperties})
                    {
                        if(list==null)continue;
                        var property=list.FirstOrDefault(v=>v.Id==id);
                        if(property==null)list.Add(new FightPropertyValue {Id=id,Value=value});else property.Value=value;
                    }
                };
                foreach(var unit in checkpoint.Units)
                {
                    unit.Hp=unit.MaxHp=unit.Turn.Hp=20000;set(unit,"MaxHp",20000);
                    if (disperse) { unit.MoveDist=0;set(unit,"MoveDist",0); }
                    set(unit,"DodgePercent",!blind && !replay && !disperse && unit.MonsterId>0?10000:0);
                    set(unit,"BlindingPercent",blind && unit.Id==1?10000:0);
                }
                foreach(var skill in player.Skills)
                {
                    skill.HasCustomDamage=true;skill.CustomDamage=100;skill.ElementType=101;
                    foreach(var hit in skill.HitGraphEntities)if(hit.DamageId>0)hit.DamageId=10004;
                    foreach(var hit in skill.Hits)if(hit.DamageId>0)hit.DamageId=10004;
                }
                var control=FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
                var runner=assets.RestoreBattle(control);
                for(int i=0;i<32 && !control.Finished;i++)
                {
                    var step=runner.Step(control.Sequence,control.Counter+1);
                    if (replay && step.Event.SkillCasts.Any(v=>v.ActorId==1 && v.IsReplay))
                        return new Probe {counter=control.Counter,targetId=1,type=10,word="重置冷却"};
                    if (disperse && step.Event.DispersedUnitIds.Count>0)
                        return new Probe {counter=control.Counter,targetId=step.Event.DispersedUnitIds[0],type=12,word="驱散"};
                    var hit=step.Event.DamageEvents.FirstOrDefault(v=>v.SourceId==1 && (blind?v.Blinding:v.Dodge));
                    if(hit!=null)return new Probe {counter=control.Counter,targetId=hit.TargetId,type=blind?9:8,word=blind?"未命中":"回避"};
                }
                throw new BuildFailedException("The battle fixture did not produce the requested world tip: "+mode+".");
            }

            public bool TryVerify(FightBattleCheckpoint checkpoint)
            {
                if(verified)return true;
                if(checkpoint==null || checkpoint.Counter<counter)return false;
                var step=checkpoint.Receipts.Single(v=>v.Counter==counter).Event;
                if (type<10)
                {
                    var hit=step.DamageEvents.First(v=>v.SourceId==1 && v.TargetId==targetId && (type==9?v.Blinding:v.Dodge));
                    Require(hit.Damage==0 && hit.ShieldDamage==0 && hit.BeforeHp==hit.AfterHp,"Avoided hit changed HP or shield.");
                }
                else if (type==10) Require(step.SkillCasts.Any(v=>v.ActorId==1 && v.IsReplay),"Live replay receipt is absent.");
                else
                {
                    Require(step.DispersedUnitIds.Contains(targetId),"Live disperse receipt is absent.");
                    var restored=FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
                    Require(restored.Receipts.Single(v=>v.Counter==counter).Event.DispersedUnitIds.Contains(targetId),"Disperse receipt did not persist.");
                }
                var view=UnityEngine.Object.FindObjectsOfType<OfficialBattleDamageFloat>()
                    .FirstOrDefault(v=>v.TargetId==targetId && v.EffectType==type && v.name.EndsWith("_Scene",StringComparison.Ordinal));
                var text=view==null?null:view.GetComponentsInChildren<TextMeshPro>().FirstOrDefault(v=>v.text==word && v.color.a>.05f);
                Observation="counter="+checkpoint.Counter+", type="+type+", native view="+(view!=null)+", word="+word;
                if(text==null || !text.renderer.isVisible)return false;
                OfficialBattleDamageDisplayAcceptance.Probe.VerifyCameraPixels(text.renderer);
                verified=true;
                if (type==10) VerifyReplayNative(view,text);
                Debug.Log("World-tip Play Mode passed: result type="+type+", word="+word+", native world prefab and original outlined font; receipt verified.");
                return true;
            }
        }

        private static void Require(bool condition,string message){if(!condition)throw new BuildFailedException(message);}
    }
}
