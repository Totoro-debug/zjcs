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
                int[] types={4,8,9};string[] words={"免疫","回避","未命中"};
                for(int i=0;i<types.Length;i++)
                {
                    foreach(var old in root.GetComponentsInChildren<OfficialBattleDamageFloat>(true))
                        UnityEngine.Object.DestroyImmediate(old.gameObject);
                    var step=new FightBattleStepEvent();
                    var hit=new FightBattleDamageEvent {TargetId=2,ElementType=101,
                        Immunity=types[i]==4,Dodge=types[i]==8,Blinding=types[i]==9};
                    step.DamageEvents.Add(hit);
                    scene.ShowDamageFloats(step,checkpoint,OfficialBattleDamageColors.Load());
                    var view=root.GetComponentsInChildren<OfficialBattleDamageFloat>(true).Single();
                    Require(view.EffectType==types[i] && view.name.EndsWith("_Scene",StringComparison.Ordinal),"The native world tip prefab was not selected.");
                    var text=view.GetComponentsInChildren<TextMeshPro>(true).Single();
                    Require(text.text==words[i] && text.font.name.Contains("FZY4K")
                        && Mathf.Approximately(text.fontSize,36) && text.fontStyle==FontStyles.Normal
                        && Mathf.Approximately(view.StackHeight,.268f),"Original world tip text/font/height binding differs.");
                    Require(text.fontSharedMaterial.shader.name=="Zjcs/Combat/WorldTextMeshPro"
                        && text.fontMaterial.IsKeywordEnabled("OUTLINE_ON")
                        && Mathf.Approximately(text.fontMaterial.GetFloat("_OutlineWidth"),1f),"Original tip shader or outline differs.");
                    scene.Tick(.12f);
                    text.ForceMeshUpdate();
                    Require(text.color.a>.05f && text.GetComponent<Renderer>().bounds.size.sqrMagnitude>0,
                        "Original world tip animation or dynamic glyphs did not become visible: alpha="+text.color.a
                        +", bounds="+text.GetComponent<Renderer>().bounds.size);
                    Color expected=OfficialBattleDamageColors.Load().Resolve(hit,101).linear;
                    Require(Mathf.Abs(text.color.r-expected.r)<.001f && Mathf.Abs(text.color.g-expected.g)<.001f
                        && Mathf.Abs(text.color.b-expected.b)<.001f,"Tip RGB no longer follows original damage color selection.");
                    VerifyPixels(view,text,types[i]);
                }
                Debug.Log("Original world tips passed: three native prefabs, original words/font/material/outline, animation, damage color and actual raster pixels.");
            }
            finally
            {
                scene?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
                ShaderUtil.allowAsyncCompilation=previousAsync;
            }
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
                Run(); // All three native visuals also render inside Play Mode with controlled result flags.
                bool blind=Environment.GetEnvironmentVariable("ZJCS_BATTLE_TEST_WORLD_TIP_MODE")=="blind";
                var checkpoint=controller.Checkpoint;
                var assets=OfficialCombatAssetCatalog.LoadDefault();var formula=assets.LoadDamageFormula();
                var player=checkpoint.Units.Single(v=>v.Id==1);
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
                    set(unit,"DodgePercent",!blind && unit.MonsterId>0?10000:0);
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
                    var hit=step.Event.DamageEvents.FirstOrDefault(v=>v.SourceId==1 && (blind?v.Blinding:v.Dodge));
                    if(hit!=null)return new Probe {counter=control.Counter,targetId=hit.TargetId,type=blind?9:8,word=blind?"未命中":"回避"};
                }
                throw new BuildFailedException("The original formula did not produce the requested tip result.");
            }

            public bool TryVerify(FightBattleCheckpoint checkpoint)
            {
                if(verified)return true;
                if(checkpoint==null || checkpoint.Counter<counter)return false;
                var hit=checkpoint.Receipts.Single(v=>v.Counter==counter).Event.DamageEvents
                    .First(v=>v.SourceId==1 && v.TargetId==targetId && (type==9?v.Blinding:v.Dodge));
                Require(hit.Damage==0 && hit.ShieldDamage==0 && hit.BeforeHp==hit.AfterHp,"Avoided hit changed HP or shield.");
                var view=UnityEngine.Object.FindObjectsOfType<OfficialBattleDamageFloat>()
                    .FirstOrDefault(v=>v.TargetId==targetId && v.EffectType==type && v.name.EndsWith("_Scene",StringComparison.Ordinal));
                var text=view==null?null:view.GetComponentsInChildren<TextMeshPro>().FirstOrDefault(v=>v.text==word && v.color.a>.05f);
                Observation="counter="+checkpoint.Counter+", type="+type+", native view="+(view!=null)+", word="+word;
                if(text==null || !text.renderer.isVisible)return false;
                OfficialBattleDamageDisplayAcceptance.Probe.VerifyCameraPixels(text.renderer);
                verified=true;
                Debug.Log("World-tip Play Mode passed: original formula result type="+type+", word="+word+", no HP/shield loss, native world prefab and original outlined font.");
                return true;
            }
        }

        private static void Require(bool condition,string message){if(!condition)throw new BuildFailedException(message);}
    }
}
