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
                int[] types={4,8,9,10,11,12};string[] words={"免疫","回避","未命中","重置冷却","减少冷却","驱散"};
                for(int i=0;i<types.Length;i++)
                {
                    foreach(var old in root.GetComponentsInChildren<OfficialBattleDamageFloat>(true))
                        UnityEngine.Object.DestroyImmediate(old.gameObject);
                    var step=new FightBattleStepEvent();
                    var hit=new FightBattleDamageEvent {TargetId=2,ElementType=101,
                        Immunity=types[i]==4,Dodge=types[i]==8,Blinding=types[i]==9};
                    if (types[i]==10) step.SkillCasts.Add(new FightBattleSkillCastEvent {ActorId=2,SkillEntityId=13220,IsReplay=true});
                    else if (types[i]==11) step.StatusCustomActions.Add(new FightBattleStatusCustomActionEvent {UnitId=2,Action=3});
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
                    if (types[i]==11) VerifyCustomActionNative(view,3);
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
                foreach(int action in new[] {1,2})
                {
                    foreach(var old in root.GetComponentsInChildren<OfficialBattleDamageFloat>(true)) UnityEngine.Object.DestroyImmediate(old.gameObject);
                    var step=new FightBattleStepEvent();
                    step.StatusCustomActions.Add(new FightBattleStatusCustomActionEvent {UnitId=2,Action=action});
                    scene.ShowDamageFloats(step,checkpoint,OfficialBattleDamageColors.Load());
                    var view=root.GetComponentsInChildren<OfficialBattleDamageFloat>(true).Single();
                    var text=view.GetComponentsInChildren<TextMeshPro>(true).Single();
                    Require(text.text==(action==1?"状态免疫":"霸体"),"Original custom action localization differs.");
                    VerifyCustomActionNative(view,action);scene.Tick(.12f);text.ForceMeshUpdate();
                    VerifyPixels(view,text,110+action);
                }
                if (Application.isPlaying) VerifyWorldAnchors();
                Debug.Log("Original world tips passed: six native prefabs, all three custom actions, original words/font/material/outline, animation, damage color, replay flags and actual raster pixels.");
            }
            finally
            {
                scene?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
                ShaderUtil.allowAsyncCompilation=previousAsync;
            }
        }

        private static void VerifyWorldAnchors()
        {
            var root=new GameObject("WorldTipAnchorFixture");
            var source=new GameObject("FixtureEntityModel");source.transform.SetParent(root.transform,false);
            OfficialBattleSceneView scene=null;
            Camera camera=Camera.main;float oldSize=camera.orthographicSize;
            try
            {
                root.transform.position=new Vector3(0,0,3);
                var mapper=new OfficialBattleGridMapper(root.transform,new FightBattleGrid {Width=4,Height=4});
                scene=new OfficialBattleSceneView(OfficialBattleModelCatalog.LoadDefault(),root.transform,_=>null,
                    mapper,(_,__)=>null,(_,__)=>{},OfficialBattleHudCatalog.LoadDefault(),root.transform);
                var checkpoint=new FightBattleCheckpoint();
                var unit=new FightBattleUnitState {Id=1,CharacterType=1,Hp=100,MaxHp=100};checkpoint.Units.Add(unit);
                scene.Refresh(checkpoint);scene.AttachUnitTransform(1,source.transform);
                Transform body=root.transform.Find("Battle attached unit 3100");
                Require(body!=null,"The anchor fixture did not attach through the real scene view.");
                var movement=new FightBattleStepEvent {ActorId=1};
                var path=new FightBattleMovementEvent {UnitId=1,MoveDuration=.5f};
                path.Path.Add(new FightGridPoint(0,0));path.Path.Add(new FightGridPoint(1,0));path.Path.Add(new FightGridPoint(2,0));
                movement.Movements.Add(path);unit.X=2;scene.Refresh(checkpoint,movement);
                var firstStep=new FightBattleStepEvent();firstStep.DispersedUnitIds.Add(1);
                scene.ShowDamageFloats(firstStep,checkpoint,OfficialBattleDamageColors.Load());
                var first=root.GetComponentsInChildren<OfficialBattleDamageFloat>(true).Single();
                Vector3 firstPosition=first.transform.position;
                Require(Mathf.Abs(firstPosition.x-body.position.x)<.0001f && Mathf.Abs(first.transform.localPosition.z)<.0001f,
                    "A new world tip used the final grid or retained local Z.");
                scene.Tick(.25f);
                Require(Mathf.Abs(body.position.x-firstPosition.x)>.01f
                    && (first.transform.position-firstPosition).sqrMagnitude<.000001f,
                    "An existing world tip followed movement without an original offset update.");
                scene.ShowDamageFloats(firstStep,checkpoint,OfficialBattleDamageColors.Load());
                var tips=root.GetComponentsInChildren<OfficialBattleDamageFloat>(true);
                var newest=tips.Single(v=>v!=first);
                float spacing=OfficialBattleDamageColors.Load().Spacing.y;
                Require(Mathf.Abs(first.transform.position.x-body.position.x)<.0001f
                    && Mathf.Abs(newest.transform.position.x-body.position.x)<.0001f
                    && Mathf.Abs(first.transform.position.y-newest.transform.position.y-newest.StackHeight-spacing)<.0001f,
                    "Adding a world tip did not re-anchor older tips at the entity's current position.");
                camera.orthographicSize=2.5f;float alpha=newest.GetComponentInChildren<TextMeshPro>().color.a;
                newest.Tick(0f);
                Require((newest.transform.localScale-Vector3.one*.5f).sqrMagnitude<.000001f
                    && Mathf.Approximately(newest.GetComponentInChildren<TextMeshPro>().color.a,alpha),
                    "A paused world tip failed to resize independently of its animation clock.");
                var delayed=new FightBattleStepEvent();
                delayed.DamageEvents.Add(new FightBattleDamageEvent {SourceId=2,TargetId=1,Damage=-10,Delay=.2f});
                scene.ShowDamageFloats(delayed,checkpoint,OfficialBattleDamageColors.Load());
                Require(root.GetComponentsInChildren<OfficialBattleDamageFloat>(true).Length==2,"A delayed number appeared early.");
                scene.Tick(.2f);
                var number=root.GetComponentsInChildren<OfficialBattleDamageFloat>(true).Single(v=>v.EffectType==0);
                Require(Mathf.Abs(number.transform.position.x-body.position.x)<.0001f
                    && Mathf.Abs(body.position.x-mapper.GridToWorld(new FightGridPoint(2,0)).x)>.01f
                    && Mathf.Abs(number.transform.localPosition.z)<.0001f,
                    "A delayed world number did not anchor at its entity's interpolated position.");
                Debug.Log("Original world float anchors passed: real attachment/movement, creation and stacking offsets, delayed numeric anchor, local Z and paused camera scale.");
            }
            finally
            {
                camera.orthographicSize=oldSize;scene?.Dispose();UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void VerifyCustomActionNative(OfficialBattleDamageFloat view,int action)
        {
            var icon=view.GetComponentsInChildren<SpriteRenderer>(true).Single();
            Require(view.EffectType==11 && icon.gameObject.activeSelf==(action==3)
                && icon.sprite.name=="Sprite_1a727abd2fbb3047" && icon.sortingOrder==3
                && icon.sharedMaterial.shader.name=="Zjcs/Combat/WorldSpriteUnlit",
                "Original custom action arrow visibility, Sprite, material or sorting differs.");
            var binding=OfficialBattleWorldTips.Find("damage_BattleCustomDamageFloatEffect");
            Require(binding.prefab.nodes.Any(v=>v.animations.Any(a=>a.name=="GameCanvas_FlyText_Immunity_Scene"
                && a.tracks.Length==6)),"Original custom action animation is incomplete.");
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
            private int counter,targetId,type,customAction;
            private string word;
            private bool verified,requiresMovement,birthAnchorObserved;
            public string Observation {get;private set;}

            public static Probe Install(OfficialBattleRuntimeController controller)
            {
                Run(); // All six native visuals also render inside Play Mode with controlled result flags.
                string mode=Environment.GetEnvironmentVariable("ZJCS_BATTLE_TEST_WORLD_TIP_MODE");
                bool movingReplay=mode=="replay-moving";
                bool blind=mode=="blind",replay=mode=="replay" || movingReplay,disperse=mode=="disperse";
                bool customCd=mode=="customcd",unflinching=mode=="unflinching";
                var checkpoint=controller.Checkpoint;
                var assets=OfficialCombatAssetCatalog.LoadDefault();var formula=assets.LoadDamageFormula();
                var player=checkpoint.Units.Single(v=>v.Id==1);
                if (customCd)
                {
                    var status=assets.Load().CreateStatus(60801,player.Id,player.Id,++checkpoint.StatusInstanceSerial);
                    FightStatusLifecycle.Apply(player,status,player.SkillState.Round,checkpoint.StatusInstanceSerial,
                        new FightRandomStream(checkpoint.RandomSeed));
                }
                if (unflinching)
                {
                    // The fixed-position fixture's NPC attacks the player; attach the status to the unit that actually loses HP.
                    var status=assets.Load().CreateStatus(25275,player.Id,player.Id,++checkpoint.StatusInstanceSerial);
                    status.UnflinchingOnDamageRate=1f;player.Statuses.Add(status);
                    var attacker=checkpoint.Units.First(v=>v.MonsterId>0);
                    player.X=attacker.X-1;player.Y=attacker.Y;
                }
                if (replay || disperse)
                {
                    var rules=assets.Load();
                    var target=checkpoint.Units.First(v=>v.MonsterId>0);
                    player.X=target.X-(movingReplay?3:1);player.Y=target.Y;
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
                    if (disperse || customCd || unflinching) { unit.MoveDist=0;set(unit,"MoveDist",0); }
                    if (movingReplay) { unit.MoveDist=unit.Id==1?2:0;set(unit,"MoveDist",unit.MoveDist); }
                    set(unit,"DodgePercent",!blind && !replay && !disperse && !customCd && !unflinching && unit.MonsterId>0?10000:0);
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
                    {
                        if (movingReplay) Require(step.Event.Movements.Any(v=>v.UnitId==1 && v.Path.Count>1),"The replay fixture did not move into skill range.");
                        var probe=new Probe {counter=control.Counter,targetId=1,type=10,word="重置冷却",requiresMovement=movingReplay};
                        if (movingReplay) controller.StepCompleted+=result=>probe.ObserveBirth(result);
                        return probe;
                    }
                    if (disperse && step.Event.DispersedUnitIds.Count>0)
                        return new Probe {counter=control.Counter,targetId=step.Event.DispersedUnitIds[0],type=12,word="驱散"};
                    if (customCd || unflinching)
                    {
                        var action=step.Event.StatusCustomActions.FirstOrDefault(v=>v.Action==(customCd?3:2));
                        if (action!=null)return new Probe {counter=control.Counter,targetId=action.UnitId,type=11,
                            word=customCd?"减少冷却":"霸体",customAction=action.Action};
                    }
                    var hit=step.Event.DamageEvents.FirstOrDefault(v=>v.SourceId==1 && (blind?v.Blinding:v.Dodge));
                    if(hit!=null)return new Probe {counter=control.Counter,targetId=hit.TargetId,type=blind?9:8,word=blind?"未命中":"回避"};
                }
                throw new BuildFailedException("The battle fixture did not produce the requested world tip: "+mode+".");
            }

            private void ObserveBirth(FightBattleExecutionResult result)
            {
                if (result.Checkpoint.Counter!=counter) return;
                var view=UnityEngine.Object.FindObjectsOfType<OfficialBattleDamageFloat>()
                    .Single(v=>v.TargetId==targetId && v.EffectType==type);
                Transform body=view.transform.parent.Find("Battle attached unit 3100");
                var prefab=OfficialBattleWorldTips.Find("damage_FightResetSkillCDFloatEffect").prefab;
                Require(body!=null && Mathf.Abs(view.transform.position.x-body.position.x-prefab.sceneOffset[0])<.0001f
                    && Mathf.Abs(view.transform.position.y-body.position.y-prefab.sceneOffset[1]-view.StackOffset.y)<.0001f
                    && Mathf.Abs(view.transform.localPosition.z)<.0001f,
                    "The moving replay tip did not initialize from the actual attached player transform.");
                birthAnchorObserved=true;
                Debug.Log("Moving world tip birth anchor passed: real StepCompleted observation matches current attached entity position.");
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
                else if (type==10)
                {
                    Require(step.SkillCasts.Any(v=>v.ActorId==1 && v.IsReplay),"Live replay receipt is absent.");
                    if (requiresMovement) Require(birthAnchorObserved && step.Movements.Any(v=>v.UnitId==targetId && v.Path.Count>1),
                        "The live moving replay tip was not observed at its entity's current position.");
                }
                else if (type==11)
                {
                    Require(step.StatusCustomActions.Any(v=>v.UnitId==targetId && v.Action==customAction),"Live custom action receipt is absent.");
                    var restored=FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
                    Require(restored.Receipts.Single(v=>v.Counter==counter).Event.StatusCustomActions
                        .Any(v=>v.UnitId==targetId && v.Action==customAction),"Custom action receipt did not persist.");
                }
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
                if (type==11) VerifyCustomActionNative(view,customAction);
                Debug.Log("World-tip Play Mode passed: result type="+type+", word="+word+", native world prefab and original outlined font; receipt verified.");
                return true;
            }
        }

        private static void Require(bool condition,string message){if(!condition)throw new BuildFailedException(message);}
    }
}
