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
            foreach(int skillId in new[]{10710,10770,11140})
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
                var rules=OfficialCombatAssetCatalog.LoadDefault().LoadSkillItems();
                var executor=FightBattleExecutor.Begin(state,OfficialCombatAssetCatalog.LoadDefault().LoadDamageFormula(),new FightDamageSettlement(),catalog,rules);
                var mapper=new OfficialBattleGridMapper(root.transform,state.Grid);
                var modelCatalog=ScriptableObject.CreateInstance<OfficialBattleModelCatalog>();
                using var scene=new OfficialBattleSceneView(modelCatalog,root.transform,_=>null,mapper,(_,__)=>"idle_01",(_,__)=>{},nativePlayableScope:scope);
                using var entities=new OfficialBattleSkillEntityPlayback(root.transform,mapper,scope,id=>state.Units.Find(u=>u.Id==id));
                using var graphs=new OfficialBattleSkillGraphPlayback(OfficialBattleSkillGraphCatalog.TryLoadDefault(),scope,entities);
                typeof(OfficialBattleSceneView).GetMethod("BindUnitEndClock",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(scene,new object[]{graphs});
                scene.Refresh(state);
                var step=executor.Step(0,1);
                var spawns=step.Event.SkillCasts.SelectMany(c=>c.HitSelections.SelectMany(h=>h.SpawnedUnits.Select(s=>new {Cast=c,Spawn=s}))).ToArray();
                Require(spawns.Length==(skillId==11140?2:1),"Original summon fixture produced no expected roles: "+skillId);
                string saved=FightBattleCheckpointSerializer.Save(state);
                scene.Refresh(state,step.Event);
                float due=spawns.Min(s=>s.Cast.Delay+s.Spawn.Delay);
                Require(due>0 && spawns.All(s=>!scene.TryGetUnitTransform(s.Spawn.UnitId)),"Summon appeared at eager settlement time");
                graphs.Tick(due-.001f);scene.Tick(0);
                Require(spawns.All(s=>!scene.TryGetUnitTransform(s.Spawn.UnitId)),"Summon appeared before its original scope time");
                graphs.Tick(.001f);
                foreach(var spawn in spawns)
                {
                    var body=scene.TryGetUnitTransform(spawn.Spawn.UnitId);
                    Require(body && Vector3.Distance(body.position,mapper.GridToWorld(new FightGridPoint(spawn.Spawn.X,spawn.Spawn.Y)))<.0001f,
                        "Summon did not appear at its captured original grid cell");
                }
                // Step refresh cannot restart a queued or already dispatched birth.
                scene.Refresh(state,step.Event);
                int birthFrames=skillId==11140?36:18;
                for(int frame=0;frame<birthFrames;frame++){scene.Tick(1f/60);graphs.Tick(1f/60);graphs.LateTick();yield return null;}
                var first=scene.TryGetUnitTransform(spawns[0].Spawn.UnitId).GetComponentInChildren<BattleNativeMonsterRenderer>();
                Require(first && first.Animation=="generate_01","Original generation graph was replaced by idle");
                using var capture=new Capture(first.transform.parent.parent,first.GetComponent<MeshRenderer>());
                capture.Save("summon-"+skillId+"-generate.png");
                for(int frame=0;frame<120;frame++){scene.Tick(1f/60);graphs.Tick(1f/60);graphs.LateTick();yield return null;}
                Require(first.Animation=="idle_01","Original generation did not settle to idle");
                capture.Save("summon-"+skillId+"-idle.png");
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
