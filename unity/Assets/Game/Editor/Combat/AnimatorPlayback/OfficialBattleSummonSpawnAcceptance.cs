using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Zjcs.Combat.Official;
using Zjcs.Offline.BattleNativePlayables;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.Combat
{
    [InitializeOnLoad]
    public static class OfficialBattleSummonSpawnAcceptance
    {
        private const string Key = "Zjcs.SummonSpawnAcceptance";
        static OfficialBattleSummonSpawnAcceptance()
        {
            EditorApplication.playModeStateChanged += state => {
                if (!SessionState.GetBool(Key, false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode) new GameObject("Summon spawn validation").AddComponent<Runner>();
                if (state == PlayModeStateChange.EnteredEditMode)
                {
                    SessionState.SetBool(Key,false);
                    EditorApplication.delayCall += () => EditorApplication.Exit(SessionState.GetInt(Key+".exit",1));
                }
            };
        }
        public static void Run()
        {
            // Reuse existing play views, with no native window creation or focus.
            var type=typeof(EditorWindow).Assembly.GetType("UnityEditor.PlayModeView",true);
            var property=type.GetProperty("enterPlayModeBehavior",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
            foreach(var view in Resources.FindObjectsOfTypeAll(type)) property.SetValue(view,Enum.Parse(property.PropertyType,"PlayUnfocused"));
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            SessionState.SetBool(Key,true);SessionState.SetInt(Key+".exit",1);AudioListener.volume=0;
            EditorApplication.isPlaying=true;
        }
        private sealed class Runner : MonoBehaviour
        {
            private IEnumerator Start()
            {
                Application.runInBackground=true;AudioListener.volume=0;Time.timeScale=0;
                string error=null;
                var routine=Check();
                while(true)
                {
                    bool more=false;object current=null;
                    try {more=routine.MoveNext();if(more)current=routine.Current;}
                    catch(Exception failure) {error=failure.ToString();Debug.LogException(failure);}
                    if(!more)break;
                    yield return current;
                }
                (routine as IDisposable)?.Dispose();Time.timeScale=1;
                File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),"summon-spawn-result.json"),
                    JsonUtility.ToJson(new Result {passed=error==null,error=error},true));
                SessionState.SetInt(Key+".exit",error==null?0:1);EditorApplication.isPlaying=false;
            }
        }
        [Serializable] private sealed class Result {public bool passed;public string error;}
        private static IEnumerator Check()
        {
            foreach(int skillId in new[]{10710,10770,11140,12060,13300,13670})
            foreach(bool fallback in skillId>=12060?new[]{false,true}:new[]{false})
            foreach(bool removed in new[]{false,true})
            foreach(int lifeMode in removed?new[]{0}:fallback?new[]{0,1,2}:new[]{0,1,2,3})
            foreach(bool nativeClock in removed || lifeMode>0 && skillId==12060 && !fallback?new[]{true,false}:new[]{true})
            foreach(float removalOffset in removed && skillId==12060 && !fallback?new[]{1.5f,0f,-.001f}:new[]{1.5f})
            {
                var root=new GameObject("Actual summon scene "+skillId);
                using var scope=new OfflineBattleNativePlayableScope(root.transform,null);
                var catalog=OfficialCombatAssetCatalog.LoadDefault().Load();
                var state=new FightBattleCheckpoint {StageId=1,RandomSeed=11,Grid=new FightBattleGrid {Width=16,Height=16,DefaultGroundType=0}};
                for(int id=1;id<=2;id++)
                {
                    var unit=new FightBattleUnitState {Id=id,Side=id,CharacterType=id==1?1:2,X=id==1?5:8,Y=5,
                        Hp=1000000,MaxHp=1000000,LastMaxHp=1000000,Level=150,SubRank=1,MoveMask=5,Direction=3,HasFlipX=true};
                    unit.Turn.Id=id;unit.Turn.Hp=unit.Hp;unit.Turn.Speed=100-id;
                    foreach(var p in new[]{new[]{1,10001},new[]{2,10003},new[]{3,1000000},new[]{4,100-id}})
                        unit.Properties.Add(new FightPropertyValue {Id=p[0],Value=p[1]});
                    state.Units.Add(unit);
                }
                var skill=catalog.CreateSkill(skillId,0,0,5,20,1,null);
                state.Units[0].Skills.Add(skill);state.Units[0].SkillState.Slots.Add(new FightSkillSlotState {Slot=0,LimitedTimes=-1});
                var wave=new FightBattleWaveState();wave.UnitIds.Add(2);state.Waves.Add(wave);
                if(skillId==12060)
                {
                    state.Units[1].Side=1;
                    var enemy=new FightBattleUnitState {Id=3,Side=2,CharacterType=2,X=14,Y=14,Hp=1000000,MaxHp=1000000};
                    enemy.Turn.Id=3;enemy.Turn.Hp=enemy.Hp;enemy.Turn.Speed=1;state.Units.Add(enemy);wave.UnitIds.Add(3);
                }
                if(fallback)
                    for(int x=0;x<16;x++) for(int y=0;y<16;y++)
                        if(x!=5 || y!=5) state.Grid.BlockedPositions.Add(new FightGridPoint(x,y));
                var rules=OfficialCombatAssetCatalog.LoadDefault().LoadSkillItems();
                var executor=FightBattleExecutor.Begin(state,OfficialCombatAssetCatalog.LoadDefault().LoadDamageFormula(),new FightDamageSettlement(),catalog,rules);
                var mapper=new OfficialBattleGridMapper(root.transform,state.Grid);
                var modelCatalog=ScriptableObject.CreateInstance<OfficialBattleModelCatalog>();
                using var scene=new OfficialBattleSceneView(modelCatalog,root.transform,_=>null,mapper,(_,__)=>"idle_01",(_,__)=>{},nativePlayableScope:scope);
                using var entities=new OfficialBattleSkillEntityPlayback(root.transform,mapper,scope,id=>state.Units.Find(u=>u.Id==id));
                using var graphs=new OfficialBattleSkillGraphPlayback(OfficialBattleSkillGraphCatalog.TryLoadDefault(),scope,entities);
                if(nativeClock) typeof(OfficialBattleSceneView).GetMethod("BindUnitEndClock",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(scene,new object[]{graphs});
                void Advance(float elapsed) {scene.Tick(elapsed);if(nativeClock)graphs.Tick(elapsed);graphs.LateTick();}
                scene.Refresh(state);
                var step=executor.Step(0,1);
                var spawns=step.Event.SkillCasts.SelectMany(c=>c.HitSelections.SelectMany(h=>h.SpawnedUnits.Select(s=>new {Cast=c,Spawn=s}))).ToArray();
                Require(spawns.Length==(skillId==11140?2:1),"Original summon fixture produced no expected roles: "+skillId);
                if(fallback) Require(spawns.All(s=>s.Spawn.X==5 && s.Spawn.Y==5),"Confirmed caster-cell fallback differs");
                float due=spawns.Min(s=>s.Cast.Delay+s.Spawn.Delay);
                float lifeDeathAge=(skillId==11140?36:18)/60f+.02f;
                if(removed)
                    foreach(var spawn in spawns)
                    {
                        // Controlled settled state: exercise the real scene using
                        // the original birth receipt and an explicit removal boundary.
                        state.Units.Find(u=>u.Id==spawn.Spawn.UnitId).Ended=true;
                        step.Event.UnitEndEvents.Add(new FightBattleUnitEndEvent {UnitId=spawn.Spawn.UnitId,Delay=due+removalOffset});
                    }
                if(lifeMode>0)
                    foreach(var spawn in spawns)
                    {
                        var unit=state.Units.Find(u=>u.Id==spawn.Spawn.UnitId);
                        long hp=unit.Hp;
                        unit.Hp=lifeMode==1?0:1;
                        step.Event.UnitLifeEvents.Add(new FightBattleUnitLifeEvent {UnitId=unit.Id,IsDead=true,
                            BeforeHp=hp,BeforeMaxHp=unit.MaxHp,Hp=0,MaxHp=unit.MaxHp,Delay=due+lifeDeathAge});
                        if(lifeMode>=2) step.Event.UnitLifeEvents.Add(new FightBattleUnitLifeEvent {UnitId=unit.Id,
                            BeforeHp=0,BeforeMaxHp=unit.MaxHp,Hp=1,MaxHp=unit.MaxHp,Delay=due+lifeDeathAge+(lifeMode==3?1.25f:.75f)});
                    }
                string saved=FightBattleCheckpointSerializer.Save(state);
                scene.Refresh(state,step.Event);
                Require(due>0 && spawns.All(s=>!scene.TryGetUnitTransform(s.Spawn.UnitId)),"Summon appeared at eager settlement time");
                Advance(due-.001f);
                Require(spawns.All(s=>!scene.TryGetUnitTransform(s.Spawn.UnitId)),"Summon appeared before its original scope time");
                // Check just after birth at the next representable float time;
                // segmented native clocks can retain a fraction of one ULP.
                float afterDue=BitConverter.ToSingle(BitConverter.GetBytes(BitConverter.ToInt32(BitConverter.GetBytes(due),0)+1),0);
                Advance(afterDue-(due-.001f));
                if(removed && removalOffset<=0)
                {
                    Require(spawns.All(s=>!scene.TryGetUnitTransform(s.Spawn.UnitId)),"Removal before/at birth left a visible model");
                    scene.Refresh(state,step.Event);Advance(1);
                    Require(spawns.All(s=>!scene.TryGetUnitTransform(s.Spawn.UnitId)),"Earlier removal was re-enqueued while birth was pending");
                    Require(FightBattleCheckpointSerializer.Save(state)==saved,"Boundary presentation changed settlement or save data");
                    Debug.Log("[Summon spawn] Removal offset="+removalOffset+" native clock="+nativeClock+": no late birth or resurrection.");
                    scene.Dispose();graphs.Dispose();scope.Dispose();UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.Destroy(modelCatalog);
                    continue;
                }
                foreach(var spawn in spawns)
                {
                    var body=scene.TryGetUnitTransform(spawn.Spawn.UnitId);
                    Require(body && Vector3.Distance(body.position,mapper.GridToWorld(new FightGridPoint(spawn.Spawn.X,spawn.Spawn.Y)))<.0001f,
                        "Summon did not appear at its captured original grid cell: "+skillId+" due="+due.ToString("R")
                        +" actual="+(body?body.position.ToString("R"):"pending"));
                }
                // Step refresh cannot restart a queued or already dispatched birth.
                scene.Refresh(state,step.Event);
                int birthFrames=skillId==11140?36:18;
                for(int frame=0;frame<birthFrames;frame++){Advance(1f/60);yield return null;}
                var first=scene.TryGetUnitTransform(spawns[0].Spawn.UnitId).GetComponentInChildren<BattleNativeMonsterRenderer>();
                Require(first && first.Animation=="generate_01","Original generation graph was replaced by idle");
                using var capture=new Capture(first.transform.parent.parent,first.GetComponent<MeshRenderer>());
                string prefix="summon-"+skillId+(fallback?"-fallback":"")+(removed?"-removed":"")
                    +(lifeMode==1?"-dead":lifeMode==2?"-revived":lifeMode==3?"-recreated":"")+(nativeClock?"":"-scene-clock");
                capture.Save(prefix+"-generate.png");
                if(lifeMode>0)
                {
                    Advance(.01f);yield return null;
                    scene.Refresh(state,step.Event);
                    Require(spawns.All(s=>scene.TryGetUnitTransform(s.Spawn.UnitId)),"Final HP removed a newborn before its lethal hit");
                    Require(first.Animation=="generate_01","Death fixture did not overlap the original generation animation");
                    Advance(.02f);yield return null;
                    Require(first.Animation=="die_01","Confirmed lethal hit did not trigger the original death graph");
                    capture.Save(prefix+"-die.png");
                    scene.Refresh(state,step.Event);
                    if(lifeMode>=2)
                    {
                        if(lifeMode==3)
                        {
                            Advance(.99f);Advance(.02f);yield return null;
                            Require(spawns.All(s=>!scene.TryGetUnitTransform(s.Spawn.UnitId)),"Death model survived its original one-second destroy delay");
                            scene.Refresh(state,step.Event);
                            Require(spawns.All(s=>!scene.TryGetUnitTransform(s.Spawn.UnitId)),"Final positive HP recreated a corpse before revival");
                            Advance(.25f);
                        }
                        else Advance(.75f);
                        yield return null;
                        scene.Refresh(state,step.Event);
                        var revived=scene.TryGetUnitTransform(spawns[0].Spawn.UnitId).GetComponentInChildren<BattleNativeMonsterRenderer>();
                        Require(revived && revived.Animation=="idle_01","Confirmed revival did not stop the death graph");
                        if(lifeMode==3) Require(!ReferenceEquals(first,revived),"Late revival reused a released native model");
                        Require(spawns.All(s=>scene.TryGetUnitTransform(s.Spawn.UnitId)
                            && scene.TryGetUnitTransform(s.Spawn.UnitId).GetComponentInChildren<BattleNativeMonsterRenderer>().Animation=="idle_01"),
                            "A revived model replayed generation or retained death");
                        using var revivedCapture=new Capture(revived.transform.parent.parent,revived.GetComponent<MeshRenderer>());
                        revivedCapture.Save(prefix+"-idle.png");
                    }
                    else
                    {
                        Advance(10f);yield return null;
                        Require(spawns.All(s=>!scene.TryGetUnitTransform(s.Spawn.UnitId)),"Original death destruction delay left a corpse model");
                    }
                    Require(FightBattleCheckpointSerializer.Save(state)==saved,"Life projection changed settlement or saved receipts");
                    Debug.Log("[Summon spawn] Actual "+skillId+": confirmed lifeMode="+lifeMode+" nativeClock="+nativeClock+"; original birth/death/revival and immutable checkpoint passed.");
                    scene.Dispose();graphs.Dispose();scope.Dispose();UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.Destroy(modelCatalog);
                    continue;
                }
                if(removed)
                {
                    float beforeRemoval=1.5f-birthFrames/60f-.01f;
                    Advance(beforeRemoval);yield return null;
                    Require(spawns.All(s=>scene.TryGetUnitTransform(s.Spawn.UnitId)),"Settled Ended flag removed a role before its receipt");
                    scene.Refresh(state,step.Event);
                    capture.Save(prefix+"-before-removal.png");
                    Advance(.02f);yield return null;
                    Require(spawns.All(s=>!scene.TryGetUnitTransform(s.Spawn.UnitId)),"New role survived its recorded removal boundary");
                    scene.Refresh(state,step.Event);Advance(0);
                    Require(spawns.All(s=>!scene.TryGetUnitTransform(s.Spawn.UnitId)),"Repeated receipt resurrected a removed role");
                    Require(FightBattleCheckpointSerializer.Save(state)==saved,"Removal presentation changed settlement or save data");
                    Debug.Log("[Summon spawn] Actual "+skillId+": controlled same-step final Ended state retained birth and removed on receipt.");
                    scene.Dispose();graphs.Dispose();scope.Dispose();UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.Destroy(modelCatalog);
                    continue;
                }
                for(int frame=0;frame<120;frame++){Advance(1f/60);yield return null;}
                Require(first.Animation=="idle_01","Original generation did not settle to idle");
                capture.Save(prefix+"-idle.png");
                Require(FightBattleCheckpointSerializer.Save(state)==saved,"Appearance modified settled combat or save data");
                Debug.Log("[Summon spawn] Actual "+skillId+": delayed native model, original generation/idle, grid position and repeat refresh passed.");
                scene.Dispose();graphs.Dispose();scope.Dispose();UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.Destroy(modelCatalog);
            }
        }
        private sealed class Capture : IDisposable
        {
            private readonly RenderPipelineAsset previous=GraphicsSettings.defaultRenderPipeline,quality=QualitySettings.renderPipeline;
            private readonly Renderer2DData data=ScriptableObject.CreateInstance<Renderer2DData>();
            private readonly UniversalRenderPipelineAsset pipeline;
            private readonly GameObject root=new GameObject("Summon capture");
            private readonly Camera camera;
            private readonly Renderer model;
            private readonly RenderTexture target=new RenderTexture(450,600,24);
            internal Capture(Transform parent,Renderer model)
            {
                this.model=model;
                pipeline=UniversalRenderPipelineAsset.Create(data);GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;
                var light=root.AddComponent<Light2D>();light.lightType=Light2D.LightType.Global;light.intensity=1;
                camera=new GameObject("Summon camera").AddComponent<Camera>();camera.transform.SetParent(root.transform,false);
                camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.65f,.7f,.75f);
                target.Create();camera.targetTexture=target;
            }
            internal void Save(string name)
            {
                camera.aspect=(float)target.width/target.height;
                var bounds=model.bounds;
                camera.transform.position=bounds.center+new Vector3(0,0,-10);
                camera.orthographicSize=Mathf.Max(.01f,Mathf.Max(bounds.extents.y,bounds.extents.x/camera.aspect)*1.5f);
                Debug.Log("[Summon capture] "+name+": "+bounds+"; camera="+camera.orthographicSize);
                camera.Render();var previousTarget=RenderTexture.active;RenderTexture.active=target;
                var pixels=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
                try {pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0);pixels.Apply();
                    File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),name),pixels.EncodeToPNG());}
                finally {RenderTexture.active=previousTarget;UnityEngine.Object.Destroy(pixels);}
            }
            public void Dispose()
            {GraphicsSettings.defaultRenderPipeline=previous;QualitySettings.renderPipeline=quality;camera.targetTexture=null;target.Release();
                UnityEngine.Object.Destroy(root);UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(pipeline);UnityEngine.Object.Destroy(data);}
        }
        private static void Require(bool value,string error){if(!value)throw new InvalidOperationException(error);}
    }
}
