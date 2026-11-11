using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Zjcs.Official.HomeExperienceRewards;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.ProfessionUI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class HomeExperienceRewardsValidation
{
    const string Key="Zjcs.HomeExperienceRewards.Active";
    static HomeExperienceRewardsValidation()
    {
        EditorApplication.playModeStateChanged+=state=>{
            if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key,false))
                new GameObject("OriginalExperienceRewardsFixture").AddComponent<HomeExperienceRewardsRunner>();
        };
        EditorApplication.update+=()=>{
            if(SessionState.GetBool(Key,false) && EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+".Deadline",float.MaxValue))
                Finish(new TimeoutException("Original experience reward validation timed out."));
        };
    }
    public static void Run()
    {
        string work=Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
        if(!Path.GetFullPath(Application.dataPath).StartsWith(work+Path.DirectorySeparatorChar))
            throw new InvalidOperationException("Owned validation project required.");
        Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION","1");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetBool(Key,true);SessionState.SetFloat(Key+".Deadline",(float)EditorApplication.timeSinceStartup+180);
        EditorApplication.EnterPlaymode();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool(Key,false);
        if(error==null)Debug.Log("HOME_EXPERIENCE_REWARDS_PASSED: original UniTask sequencing, native experience widget, parallel displays, upgrade gates, cancellation/restart, default notification and subscriptions. Controlled player and parser endpoints only.");
        else Debug.LogException(error);
        EditorApplication.Exit(error==null?0:1);
    }
}

public sealed class HomeExperienceRewardsRunner:MonoBehaviour
{
    HomeExperienceRewardHandler rewards;GameObject widget;Camera cameraUi;RenderTexture target;
    int errors;Bindings bindings;readonly List<string> frames=new List<string>();
    IEnumerator Start()
    {
        AudioListener.volume=0;
        Application.logMessageReceived+=(message,stack,type)=>{if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)errors++;};
        var steps=Check();
        for(;;){object current=null;bool next;
            try{next=steps.MoveNext();if(next)current=steps.Current;}
            catch(Exception error){if(widget)widget.SetActive(false);rewards?.Dispose();HomeExperienceRewardsValidation.Finish(error);yield break;}
            if(!next)break;yield return current;
        }
        HomeExperienceRewardsValidation.Finish(errors==0?null:new Exception("Unexpected experience reward errors: "+errors));
    }
    IEnumerator Check()
    {
        var pipeline=UniversalRenderPipelineAsset.Create(ScriptableObject.CreateInstance<Renderer2DData>());
        GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;
        cameraUi=new GameObject("ExperienceCamera",typeof(Camera)).GetComponent<Camera>();
        cameraUi.clearFlags=CameraClearFlags.SolidColor;cameraUi.backgroundColor=new Color(.12f,.2f,.28f);cameraUi.orthographic=true;
        target=new RenderTexture(Screen.width,Screen.height,24);cameraUi.targetTexture=target;
        var root=new GameObject("NativeExperienceCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
        var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=cameraUi;canvas.planeDistance=1;
        var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1080,1920);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
        bindings=new Bindings();rewards=new HomeExperienceRewardHandler(bindings);
        Require(rewards.HandledExp==0 && rewards.HandledLevel==1,"uninitialized console-data fallback reads the selected player");
        bindings.Memory.Inited=true;rewards.Activate();rewards.Activate();
        Require(bindings.PlayerData.Subscribers==6,"scene activation subscribes each original reward event once");
        var staging=new GameObject("InactiveExperienceStaging");staging.SetActive(false);
        // Clone the complete original configured slider subtree, without
        // activating unrelated avatar/player-detail components in this fixture.
        var original=OfficialHomeAvatarAssetCatalog.LoadDefault().prefab.GetComponentInChildren<HomePlayerExpSlider>(true);
        widget=Object.Instantiate(original.gameObject,staging.transform,false);widget.SetActive(false);
        var exp=widget.GetComponent<HomePlayerExpSlider>();
        var services=new Experience(bindings,rewards);exp.Configure(services);
        widget.transform.SetParent(canvas.transform,false);widget.SetActive(true);Object.Destroy(staging);
        yield return null;yield return null;
        var slider=widget.GetComponentInChildren<Slider>(true);
        Require(bindings.PlayerData.Subscribers==11 && exp.Level==1 && Math.Abs(slider.value-9)<.01f,"native widget binds the real handler and original empty arc");
        var updates=new List<string>();Action<long,long> update=(pre,post)=>updates.Add(pre+":"+post);
        rewards.OnPlayerExpUpdated.AddListener(update);rewards.OnPlayerExpUpdated.AddListener(update);
        Func<long,long,IEnumerator> first=(pre,post)=>Probe("A",pre,post,2);
        Func<long,long,IEnumerator> second=(pre,post)=>Probe("B",pre,post,3);
        rewards.RegisterRewardExpDisplay(first);rewards.RegisterRewardExpDisplay(second);
        bool blocked=false;Func<bool> gate=()=>blocked;
        rewards.RegisterUpgradeDelay(gate);yield return new WaitForSeconds(.1f);
        int upgraded=0;Action<int,int> upgrade=(pre,post)=>{
            Require(exp.Level==2 && !blocked && pre==1 && post==2,"upgrade display follows the completed ring and all gates");upgraded++;
        };
        rewards.RegisterUpgradeDisplay(upgrade,.05f);frames.Clear();
        blocked=true;bindings.PlayerData.Gain(150,2);
        yield return null;yield return null;
        Require(rewards.HandledExp==150 && bindings.Blockers.Active && upgraded==0,"handled experience is captured before the animation; rating stays blocked");
        yield return new WaitForSeconds(1.25f);
        Require(upgraded==0 && rewards.IsUpgrade && exp.Level==2 && Math.Abs(slider.value-159)<.02f,"native ring finishes but the original upgrade gate still waits");
        Require(updates.SequenceEqual(new[]{"0:150"}) && frames.Take(2).SequenceEqual(new[]{"B:0:150:0","A:0:150:0"}),"safe signal deduplicates; display enumerators advance in reverse registration order");
        Capture("home-experience-reward-gated.png");
        blocked=false;yield return new WaitForSeconds(.2f);
        Require(upgraded==1 && rewards.HandledLevel==2 && !rewards.IsUpgrade && !bindings.Blockers.Active && bindings.Notifications.Count==0,"last registered upgrade display wins and releases the blocker after its duration");
        rewards.UnregisterUpgradeDisplay(upgrade);
        frames.Clear();bindings.PlayerData.Gain(200,2);yield return new WaitForSeconds(.25f);
        bindings.PlayerData.Gain(250,2);yield return new WaitForSeconds(1.25f);
        Require(frames.Any(row=>row=="A:150:200:0") && frames.Any(row=>row=="A:200:250:0"),"a new award cancels the prior playback and resumes from original handled experience");
        Require(rewards.HandledExp==250 && Math.Abs(slider.value-241)<.02f && !bindings.Blockers.Active,"restarted native animation reaches the latest award without repeating the prior range");
        rewards.UnregisterRewardExpDisplay(first);rewards.UnregisterRewardExpDisplay(second);
        widget.SetActive(false);yield return null;
        Require(bindings.PlayerData.Subscribers==6,"widget deactivation removes only its five subscriptions");
        bindings.PlayerData.Gain(260,2);yield return new WaitForSeconds(.1f);
        Require(rewards.HandledExp==250 && !bindings.Blockers.Active,"without a display pending experience stays unhandled");
        widget.SetActive(true);yield return new WaitForSeconds(1.25f);
        Require(rewards.HandledExp==260 && Math.Abs(slider.value-249.2f)<.03f,"reopening the original widget consumes the pending range");
        bindings.PlayerData.Gain(280,2);yield return new WaitForSeconds(.2f);
        bindings.OnPlayerUpdate.Invoke();yield return new WaitForSeconds(.1f);
        Require(rewards.HandledExp==280 && !bindings.Blockers.Active,"player update cancels the active flow and releases rating blocker");
        widget.SetActive(false);yield return null;widget.SetActive(true);yield return null;
        bindings.PlayerData.Gain(350,3);yield return new WaitForSeconds(1.3f);
        Require(bindings.Notifications.SequenceEqual(new[]{2}) && rewards.HandledLevel==3 && bindings.Blockers.Active,
            "default level notification receives the previous level and keeps its original 2.2-second display interval");
        Require(exp.Level==3 && Math.Abs(slider.value-368)<.03f,"native widget crosses the next level before the default notification");
        Capture("home-experience-reward-upgrade.png");
        widget.SetActive(false);yield return null;rewards.Dispose();rewards.Dispose();
        Require(bindings.PlayerData.Subscribers==0 && !bindings.Blockers.Active && bindings.Blockers.Added==bindings.Blockers.Removed,"scene disposal removes all events and cancels the default notification wait");
        int registrations=bindings.Blockers.Added;bindings.PlayerData.Gain(400,3);bindings.OnPlayerUpdate.Invoke();
        yield return new WaitForSeconds(.1f);
        Require(bindings.Blockers.Added==registrations && bindings.Notifications.Count==1,"disposed scene receives no further awards or manager callbacks");
        rewards=null;
    }
    IEnumerator Probe(string name,long pre,long post,int count)
    {
        for(int index=0;index<count;index++){frames.Add(name+":"+pre+":"+post+":"+index);yield return null;}
    }
    void Capture(string name)
    {
        Canvas.ForceUpdateCanvases();cameraUi.Render();var previous=RenderTexture.active;RenderTexture.active=target;
        var texture=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
        texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();
        File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),name),texture.EncodeToPNG());
        RenderTexture.active=previous;Object.Destroy(texture);
    }
    static void Require(bool value,string message){if(!value)throw new Exception(message);}
    sealed class Memory:IHomeExperienceRewardStore
    {public bool Inited{get;set;}public long HandledExp{get;set;}public int HandledLevel{get;set;}=1;}
    sealed class Blocker:IHomeExperienceCombatRatingBlockers
    {
        public bool Active;public int Added,Removed;
        public void RegisterBlocker(HomeExperienceRewardHandler value){Require(!Active,"unique rating blocker");Active=true;Added++;}
        public void UnregisterBlocker(HomeExperienceRewardHandler value){Require(Active,"matching rating blocker release");Active=false;Removed++;}
    }
    sealed class Bindings:IHomeExperienceRewardBindings
    {
        public readonly Player PlayerData=new Player();public readonly Memory Memory=new Memory();public readonly Blocker Blockers=new Blocker();
        public readonly List<int> Notifications=new List<int>();
        public IHomeExperienceRewardPlayer Player=>PlayerData;public IHomeExperienceRewardStore Store=>Memory;
        public bool ManagerAvailable=>true;public SafeAction OnPlayerUpdate{get;}=new SafeAction();
        public int PlayerExpItemId=>4;public IHomeExperienceCombatRatingBlockers CombatRatingBlockers=>Blockers;
        public void ShowLevelUpNotification(int pre)=>Notifications.Add(pre);
    }
    sealed class Experience:IHomeExperienceServices,IHomeLevelExpParser,IHomeLevelBlessParser
    {
        readonly Bindings binding;readonly HomeExperienceRewardHandler handler;
        public Experience(Bindings binding,HomeExperienceRewardHandler handler){this.binding=binding;this.handler=handler;}
        public IHomeExperiencePlayer MainPlayer=>binding.Player;public bool ManagerAvailable=>true;
        public SafeAction OnPlayerUpdate=>binding.OnPlayerUpdate;public IHomeExperienceRewards Rewards=>handler;
        public IHomeLevelExpParser LevelExpParser=>this;public IHomeLevelBlessParser LevelBlessInfoParser=>this;
        public int PlayerExpItemId=>4;public int PlanesRank=>1;public void ResolveSubRank(int value){}
        public object Compact(long value)=>value;public string GetSeasonNameByRank(int rank)=>rank.ToString();
        readonly long[] levels={0,0,100,300,600,1000};
        public long GetExp(int id,int level)=>level<=0?0:level>=levels.Length?long.MaxValue:levels[level];
        public int CalcLevelByExp(int id,long value)=>Enumerable.Range(0,levels.Length).Last(index=>levels[index]<=value);
        public long GetLevelUpExp(int id,int level)=>GetExp(id,level+1)-GetExp(id,level);
        public long GetLevelUpExp(int id,int from,int to)=>GetExp(id,to)-GetExp(id,from);
        public int GetBlessLevel(int rank,long value)=>throw new NotSupportedException();
        public HomeLevelBlessInfo Get(int rank,int level)=>throw new NotSupportedException();
        public long GetExpRequestByBlessLevel(int level)=>throw new NotSupportedException();
    }
    sealed class Player:IHomeExperienceRewardPlayer
    {
        long experience;int level=1;
        public int LevelLimit=>5;public int Rank=>1;public int BaseLevel=>level;public int Level=>level;public int SubRank=>1;
        Action<HomeExperienceItem,long,object> item;Action<int> subrank,levelUpdate;Action bless;Action<object> seasonAdd,end;
        Action<long,long> blessExp;Action<int,int> blessLevel;
        public int Subscribers=>(item?.GetInvocationList().Length??0)+(subrank?.GetInvocationList().Length??0)+(bless?.GetInvocationList().Length??0)+(seasonAdd?.GetInvocationList().Length??0)+(end?.GetInvocationList().Length??0)+(levelUpdate?.GetInvocationList().Length??0)+(blessExp?.GetInvocationList().Length??0)+(blessLevel?.GetInvocationList().Length??0);
        public event Action<HomeExperienceItem,long,object> OnItemCountUpdate{add{item+=value;}remove{item-=value;}}
        public event Action<int> OnSubRankUpdate{add{subrank+=value;}remove{subrank-=value;}}
        public event Action OnBlessLevelDataUpdate{add{bless+=value;}remove{bless-=value;}}
        public event Action<object> OnSeasonRecordDataAdd{add{seasonAdd+=value;}remove{seasonAdd-=value;}}
        public event Action<object> OnSeasonRecordDataEndUpdate{add{end+=value;}remove{end-=value;}}
        public event Action<int> OnLevelUpdate{add{levelUpdate+=value;}remove{levelUpdate-=value;}}
        public event Action<long,long> OnBlessExpUpdate{add{blessExp+=value;}remove{blessExp-=value;}}
        public event Action<int,int> OnBlessLevelUp{add{blessLevel+=value;}remove{blessLevel-=value;}}
        public void Gain(long value,int nextLevel){long pre=experience;experience=value;if(nextLevel!=level){level=nextLevel;levelUpdate?.Invoke(level);}item?.Invoke(new HomeExperienceItem{Id=4},pre,null);}
        public bool IsBlessLevelOpen()=>false;public bool ShowPlayerStoreExpLimit()=>false;
        public int GetMaxStoreExpLevel()=>5;public bool IsMaxBlessLevel()=>false;
        public long GetCurBlessPlayerExp()=>0;public int GetCurBlessPlayerLevel()=>0;public long GetItemAmount(int id)=>experience;
        public long GetCurrentExp()=>experience;
    }
}
