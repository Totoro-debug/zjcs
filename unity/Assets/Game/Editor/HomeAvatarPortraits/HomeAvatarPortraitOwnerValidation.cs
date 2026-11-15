using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Zjcs.Offline.CharacterCreation;
using Zjcs.Offline.HomeUI;
using Zjcs.Offline.HomeAvatarUI;
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.Partners;
using Zjcs.Official.ProfessionUI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class HomeAvatarPortraitOwnerValidation
{
    const string Key="Zjcs.HomeAvatarPortraitOwner.Active";
    static HomeAvatarPortraitOwnerValidation()
    {
        EditorApplication.playModeStateChanged+=state=>{
            if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key,false))
                new GameObject("OriginalHomeAvatarFixture").AddComponent<HomeAvatarPortraitOwnerRunner>();
            if(state==PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Key+".Exit",false))
            {
                SessionState.SetBool(Key+".Exit",false);
                double deadline=EditorApplication.timeSinceStartup+3;
                EditorApplication.CallbackFunction exit=null;
                exit=()=>{if(EditorApplication.timeSinceStartup<deadline || EditorApplication.isUpdating || EditorApplication.isCompiling)return;
                    EditorApplication.update-=exit;EditorApplication.Exit(SessionState.GetInt(Key+".Code",1));};
                EditorApplication.update+=exit;
            }
        };
        EditorApplication.update+=()=>{
            if(SessionState.GetBool(Key,false) && EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+".Deadline",float.MaxValue))
                Finish(new TimeoutException("Original home avatar validation timed out."));
        };
    }
    public static void Run()
    {
        string work=Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
        string repository=Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT"));
        if(!work.StartsWith(Path.Combine(repository,"output","validation")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)
            || !File.Exists(Path.Combine(work,".zjcs-validation.json")))
            throw new InvalidOperationException("Owned validation workspace required.");
        Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION","1");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetBool(Key,true);SessionState.SetFloat(Key+".Deadline",(float)EditorApplication.timeSinceStartup+240);
        EditorApplication.EnterPlaymode();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool(Key,false);
        if(error==null)Debug.Log("HOME_AVATAR_PORTRAIT_OWNER_PASSED: production home portrait owner, original studio/body/weapon/effect resource routes, actual profile, head geometry, internal pointer, controlled experience branches, reload and release. Formal HUD/reward queue is not exercised.");
        else Debug.LogException(error);
        SessionState.SetInt(Key+".Code",error==null?0:1);SessionState.SetBool(Key+".Exit",true);
        if(EditorApplication.isPlayingOrWillChangePlaymode)EditorApplication.ExitPlaymode();
        else EditorApplication.delayCall+=()=>EditorApplication.Exit(error==null?0:1);
    }
}

public sealed class HomeAvatarPortraitOwnerRunner:MonoBehaviour
{
    Camera cameraUi;RenderTexture target;EventSystem events;
    OfflineHomeAvatar avatar;OfflineHomeAvatarPortraits portraitSession;
    int errors,opened;string work;
    IEnumerator Start()
    {
        AudioListener.volume=0;work=Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
        Application.logMessageReceived+=(message,stack,type)=>{if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)errors++;};
        var steps=Check();
        for(;;){object current=null;bool next;
            try{next=steps.MoveNext();if(next)current=steps.Current;}
            catch(Exception error){avatar?.Dispose();portraitSession?.Dispose();HomeAvatarPortraitOwnerValidation.Finish(error);yield break;}
            if(!next)break;yield return current;
        }
        HomeAvatarPortraitOwnerValidation.Finish(errors==0?null:new Exception("Unexpected avatar errors: "+errors));
    }
    IEnumerator Check()
    {
        string saveRoot=Path.Combine(work,"avatar-saves-"+Guid.NewGuid().ToString("N"));
        var creation=CharacterCreationOfflineService.OpenDefault(saveRoot);
        string slot=creation.CreateSlot();var draft=creation.OpenDraft(slot);draft.name="家园头像";
        Require(creation.TryCreateCharacter(slot,draft,out string error),error);
        Require(creation.TrySetInitialWeapon(slot,CharacterCreationOfflineService.SwordInitialWeaponType,out error),error);
        var profile=OfflineMainlineProgressSession.OpenCharacter(new ProfileSlotCatalog(saveRoot),slot);
        profile.EnsureLocalActorIdentities();
        var growth=new OfflineCharacterProgressionService(profile,OfficialCharacterAssetCatalog.LoadDefault().Load());
        var pipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Official/World/Map/13/NativePipeline/UniversalRenderPipeline.asset");
        Require(pipeline,"packaged original rendering pipeline");
        GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;
        var previous=ProfessionAppearanceTextureBindings.Current;
        var images=ScriptableObject.CreateInstance<OfflineProfessionImageCatalog>();images.Entries=Array.Empty<OfflineProfessionImageCatalog.Entry>();
        using var actors=new OfflinePartnerActorUiRuntime(profile,growth,OfficialPartnerAssetCatalog.LoadDefault(),
            new OfflineProfessionImageResourceLoader(images),key=>key,_=>{throw new Exception("Home avatar requested NPC popup");},()=>opened++);
        portraitSession=new OfflineHomeAvatarPortraits(Path.Combine(work,"avatar-portrait-cache"),
            profile.Save.Player.CharacterProperties.LocalActorIdentities.PlayerId,()=>growth.Player,id=>growth.Catalog.ItemType(id));
        string initialPlayer=JsonUtility.ToJson(profile.Save.Player);
        try
        {
            cameraUi=new GameObject("AvatarCamera",typeof(Camera)).GetComponent<Camera>();
            cameraUi.clearFlags=CameraClearFlags.SolidColor;cameraUi.backgroundColor=new Color(.12f,.2f,.28f);cameraUi.orthographic=true;
            target=new RenderTexture(608,1080,24);cameraUi.targetTexture=target;
            var root=new GameObject("NativeAvatarCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=cameraUi;canvas.planeDistance=1;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1080,1920);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            events=new GameObject("InternalAvatarPointer",typeof(EventSystem)).GetComponent<EventSystem>();
            var experience=new Experience();
            avatar=new OfflineHomeAvatar(canvas.transform,actors.TeamIcons,portraitSession.Manager,experience);
            for(int frame=0;frame<90;frame++){portraitSession.Update();yield return null;}
            var raw=avatar.Root.GetComponentInChildren<RawImage>(true);
            Require(avatar.Icon.myIconMe.InitFinished && raw.texture is Texture2D,"original AutoInit binds actual selected player and captures a head texture");
            var texture=(Texture2D)raw.texture;
            Require(texture.width==100 && texture.height==120,"original Head capture dimensions");
            Require(raw.uvRect==new Rect(0,0,1,1) && raw.rectTransform.rect.size==new Vector2(100,120),"native Rect v2 preserves the full UV area and original SetNativeSize result");
            Canvas.ForceUpdateCanvases();var headMesh=raw.canvasRenderer.GetMesh();
            Require(headMesh.vertexCount>0 && headMesh.uv.All(uv=>!float.IsNaN(uv.x) && !float.IsNaN(uv.y)
                && !float.IsInfinity(uv.x) && !float.IsInfinity(uv.y)),"original capsule produces finite textured vertices");
            Require(texture.GetPixels32().Count(pixel=>pixel.a>0)>500 && texture.GetPixels32().Any(pixel=>pixel.a==0),"actual player head with transparent margins");
            File.WriteAllBytes(Path.Combine(work,"home-avatar-head.png"),texture.EncodeToPNG());
            Require(avatar.Icon.myIconMe.InfoUI.PlayerInfoData.Id==profile.Save.Player.CharacterProperties.LocalActorIdentities.PlayerId,"persisted player identity reaches original portrait controller");
            var slider=avatar.Root.GetComponentInChildren<Slider>(true);
            Require(avatar.Experience.Level==1 && Math.Abs(slider.value-9)<.01f,"original minimum visual arc at zero experience");
            Require(experience.Player.Subscribers==5 && experience.Display!=null,"all original player events and reward display registered");
            Click(avatar.Root.GetComponent<Button>());Require(opened==1,"native avatar pointer opens selected character detail");
            var animation=experience.Display(0,50);while(animation.MoveNext())yield return animation.Current;
            Require(avatar.Experience.Level==1 && Math.Abs(slider.value-50)<.01f,"actual one-second reward coroutine finishes at half of level one");
            Capture("home-avatar-native.png");
            experience.Player.Store=true;experience.Player.Limit=2;experience.Player.MaxStore=4;
            animation=experience.Display(50,400);while(animation.MoveNext())yield return animation.Current;
            Require(avatar.Experience.Level==2 && slider.minValue==100 && slider.maxValue==600 && Math.Abs(slider.value-391)<.01f,"capped level uses original stored-experience bounds");
            experience.Player.Store=false;experience.Player.Bless=true;
            animation=experience.Display(400,350);while(animation.MoveNext())yield return animation.Current;
            Require(avatar.Experience.Level==3 && slider.minValue==200 && slider.maxValue==500 && Math.Abs(slider.value-268)<.02f,"original blessing branch subtracts normal cap before mapping its arc");
            Capture("home-avatar-blessing.png");
            avatar.Root.SetActive(false);Require(experience.Player.Subscribers==0 && experience.Display==null,"deactivation releases player and reward subscriptions");
            avatar.Root.SetActive(true);yield return null;
            Require(experience.Player.Subscribers==5 && experience.Display!=null,"reactivation registers each original subscription once");
            avatar.Dispose();avatar=null;yield return null;
            Require(experience.Player.Subscribers==0 && experience.Display==null,"avatar disposal releases original widget subscriptions");
            profile.Reload();portraitSession.RefreshAppearance();
            Require(JsonUtility.ToJson(profile.Save.Player)==initialPlayer,"portrait lifetime never rewrites the saved player");
            portraitSession.Dispose();portraitSession=null;
            cameraUi.targetTexture=null;target.Release();Object.Destroy(target);
            Object.Destroy(root);Object.Destroy(cameraUi.gameObject);Object.Destroy(events.gameObject);
            yield return null;
        }
        finally{ProfessionAppearanceTextureBindings.Current=previous;}
    }
    void Click(Button button)
    {
        Canvas.ForceUpdateCanvases();var rect=(RectTransform)button.transform;
        var pointer=new PointerEventData(events){position=RectTransformUtility.WorldToScreenPoint(cameraUi,rect.TransformPoint(rect.rect.center)),button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();events.RaycastAll(pointer,hits);
        Require(hits.Count>0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)==button.gameObject,"unobstructed native avatar raycast");
        ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerClickHandler);
    }
    void Capture(string name)
    {
        Canvas.ForceUpdateCanvases();cameraUi.Render();var previous=RenderTexture.active;RenderTexture.active=target;
        var texture=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
        texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();
        File.WriteAllBytes(Path.Combine(work,name),texture.EncodeToPNG());RenderTexture.active=previous;Object.Destroy(texture);
    }
    static void Require(bool value,string message){if(!value)throw new Exception(message);}
    sealed class Experience:IHomeExperienceServices,IHomeLevelExpParser,IHomeLevelBlessParser,IHomeExperienceRewards
    {
        public PlayerData Player=new PlayerData();public Func<long,long,IEnumerator> Display;
        public IHomeExperiencePlayer MainPlayer=>Player;public bool ManagerAvailable=>true;
        public SafeAction OnPlayerUpdate{get;}=new SafeAction();
        public IHomeLevelExpParser LevelExpParser=>this;public IHomeLevelBlessParser LevelBlessInfoParser=>this;
        public IHomeExperienceRewards Rewards=>this;public int PlayerExpItemId=>4;public int PlanesRank=>1;
        public void ResolveSubRank(int value){}public object Compact(long value)=>value;public string GetSeasonNameByRank(int rank)=>rank.ToString();
        public long HandledExp=>0;public long PlayerRealExp=>0;
        public void RegisterRewardExpDisplay(Func<long,long,IEnumerator> display){Require(Display==null,"unique reward display registration");Display=display;}
        public void UnregisterRewardExpDisplay(Func<long,long,IEnumerator> display){Require(Display==display,"matching reward display removal");Display=null;}
        readonly long[] levels={0,0,100,300,600,1000};
        public long GetExp(int id,int level)=>level<=0?0:level>=levels.Length?long.MaxValue:levels[level];
        public int CalcLevelByExp(int id,long value)=>Enumerable.Range(0,levels.Length).Last(index=>levels[index]<=value);
        public long GetLevelUpExp(int id,int level)=>GetExp(id,level+1)-GetExp(id,level);
        public long GetLevelUpExp(int id,int from,int to)=>GetExp(id,to)-GetExp(id,from);
        public int GetBlessLevel(int rank,long value)=>value<200?0:value<500?1:2;
        public HomeLevelBlessInfo Get(int rank,int level)=>new HomeLevelBlessInfo{Exp=GetExpRequestByBlessLevel(level)};
        public long GetExpRequestByBlessLevel(int level)=>level<=0?0:level==1?200:500;
    }
    sealed class PlayerData:IHomeExperiencePlayer
    {
        public int Limit=5,MaxStore=5;public bool Store,Bless;
        public int LevelLimit=>Limit;public int Rank=>1;public int BaseLevel=>2;public int Level=>1;public int SubRank=>1;
        Action<HomeExperienceItem,long,object> item;Action<int> subrank;Action bless;Action<object> seasonAdd,end;
        public int Subscribers=>(item?.GetInvocationList().Length??0)+(subrank?.GetInvocationList().Length??0)+(bless?.GetInvocationList().Length??0)+(seasonAdd?.GetInvocationList().Length??0)+(end?.GetInvocationList().Length??0);
        public event Action<HomeExperienceItem,long,object> OnItemCountUpdate{add{item+=value;}remove{item-=value;}}
        public event Action<int> OnSubRankUpdate{add{subrank+=value;}remove{subrank-=value;}}
        public event Action OnBlessLevelDataUpdate{add{bless+=value;}remove{bless-=value;}}
        public event Action<object> OnSeasonRecordDataAdd{add{seasonAdd+=value;}remove{seasonAdd-=value;}}
        public event Action<object> OnSeasonRecordDataEndUpdate{add{end+=value;}remove{end-=value;}}
        public bool IsBlessLevelOpen()=>Bless;public bool ShowPlayerStoreExpLimit()=>Store;
        public int GetMaxStoreExpLevel()=>MaxStore;public bool IsMaxBlessLevel()=>false;
        public long GetCurBlessPlayerExp()=>250;public int GetCurBlessPlayerLevel()=>1;public long GetItemAmount(int itemId)=>0;
    }
}
