using System;
using System.Collections;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Zjcs.Offline.HomeLevelUpUI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.HomeLevelUpUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class HomeLevelUpWindowsValidation
{
    const string Key = "Zjcs.HomeLevelUpWindows.Active";
    static HomeLevelUpWindowsValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key,false))
                new GameObject("LevelUpOwnerFixture").AddComponent<HomeLevelUpWindowsRunner>();
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Key+".Exit",false))
            { SessionState.SetBool(Key+".Exit",false); EditorApplication.delayCall += () => EditorApplication.Exit(SessionState.GetInt(Key+".Code",1)); }
        };
        EditorApplication.update += () => {
            if (SessionState.GetBool(Key,false) && EditorApplication.timeSinceStartup > SessionState.GetFloat(Key+".Deadline",float.MaxValue))
                Finish(new TimeoutException("Production level-up owner validation timed out."));
        };
    }
    public static void Run()
    {
        string work=Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
        if (!Path.GetFullPath(Application.dataPath).StartsWith(work+Path.DirectorySeparatorChar)) throw new Exception("Owned project required.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetBool(Key,true); SessionState.SetFloat(Key+".Deadline",(float)EditorApplication.timeSinceStartup+180);
        EditorApplication.EnterPlaymode();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool(Key,false); SessionState.SetBool(Key+".Exit",true); SessionState.SetInt(Key+".Code",error==null?0:1);
        if (error==null) Debug.Log("HOME_LEVEL_UP_WINDOWS_PASSED: production Resources catalog, selected formula, duplicate tips, sorting, original animation/audio, auto-close, in-flight disposal, borrowed owners and reloaded model. Formal HUD/reward queue is not exercised.");
        else Debug.LogException(error);
        if(EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.ExitPlaymode();
        else EditorApplication.delayCall += () => EditorApplication.Exit(error==null?0:1);
    }
}

public sealed class HomeLevelUpWindowsRunner : MonoBehaviour
{
    RenderTexture target; Camera cameraUi; Canvas canvas; int errors,sounds;
    IEnumerator Start()
    {
        AudioListener.volume=0;
        Application.logMessageReceived += (m,s,t) => { if(t==LogType.Error || t==LogType.Exception || t==LogType.Assert) errors++; };
        var check=Check();
        for(;;) {
            bool next; object current=null;
            try { next=check.MoveNext(); if(next) current=check.Current; }
            catch(Exception error) { HomeLevelUpWindowsValidation.Finish(error); yield break; }
            if(!next) break; yield return current;
        }
        HomeLevelUpWindowsValidation.Finish(errors==0?null:new Exception("Unexpected errors: "+errors));
    }
    IEnumerator Check()
    {
        var assets=OfflineHomeLevelUpAssets.LoadDefault();
        Require(assets.images.Entries.Length==7,"explicit seven-entry original resource closure");
        var images=new OfflineProfessionImageResourceLoader(assets.images);
        var properties=new OfflineProfessionPropertyBindings(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Official/Partners/partner-property-ui.json"),images,assets.CreateLanguage(),
            (_,__,___,____,_____)=>throw new Exception("Original tip has no detail button"),
            (_,__,___,____,_____,______)=>throw new Exception("Original tip has no blessing button"));
        var previousProperties=ProfessionPropertyBindings.Current; ProfessionPropertyBindings.Current=properties;
        var audioCatalog=ScriptableObject.CreateInstance<OfflineProfessionAudioCatalog>();
        audioCatalog.Events=new[]{new OfflineProfessionAudioCatalog.Entry { EventId=OfflineHomeLevelUpAssets.SoundEventId,Clip=assets.sound }};
        var audioHost=gameObject.AddComponent<OfflineProfessionSoundHost>(); audioHost.Bind(audioCatalog);
        var previousSound=OfficialWwiseEvent.Host;
        var pipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Official/World/Map/13/NativePipeline/UniversalRenderPipeline.asset");
        GraphicsSettings.defaultRenderPipeline=pipeline; QualitySettings.renderPipeline=pipeline;
        cameraUi=new GameObject("LevelUpCamera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();
        cameraUi.transform.position=new Vector3(0,0,-10); cameraUi.backgroundColor=new Color(.12f,.2f,.28f); cameraUi.clearFlags=CameraClearFlags.SolidColor;
        target=new RenderTexture(608,1080,24); cameraUi.targetTexture=target;
        canvas=new GameObject("LevelUpCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)).GetComponent<Canvas>();
        canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=cameraUi; canvas.planeDistance=1; canvas.sortingOrder=40;
        var scaler=canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1080,1920); scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
        var player=new OfflinePlayerProgress { Level=2,SubRank=1 }; player.Wardrobe.Profession=1;
        player.ProfessionLevels.Add(new OfflineProfessionLevel { Profession=1,Level=1 });
        using var calculator=OfflineHomeLevelUpProperties.LoadDefault(()=>player);
        var bindings=new OfflineHomeLevelUpWindowBindings { Canvas=canvas,Properties=properties,SoundHost=audioHost,PostSound=(clip,go)=>{
            Require(clip==assets.sound,"original clip from catalog"); sounds++;
            var source=go.AddComponent<AudioSource>(); source.clip=clip; source.Play(); Require(source.isPlaying,"actual muted playback"); } };
        var owner=new OfflineHomeLevelUpWindows(bindings,calculator,()=>player.Level);
        Require(!LevelUpNotifyTip.TryPopup(owner,2) && owner.WindowCount==0,"equal level never opens");
        Require(LevelUpNotifyTip.TryPopup(owner,1),"first original notification");
        yield return new WaitForSeconds(.3f);
        Require(LevelUpNotifyTip.TryPopup(owner,1) && owner.WindowCount==2,"original duplicate flag creates independent instances");
        yield return new WaitForSeconds(.7f);
        var tips=canvas.GetComponentsInChildren<LevelUpNotifyTip>();
        Require(tips.Length==2 && tips.Select(x=>x.GetComponent<Canvas>().sortingOrder).SequenceEqual(new[]{50,60}),"original sorting increment");
        Require(tips.All(t=>t.GetComponentsInChildren<ProfessionPlayerPropItem>(true).Count(row=>row.gameObject.activeSelf)==4),"real selected formula changes all four properties");
        Require(tips.All(t=>t.GetComponentsInChildren<TMP_Text>(true).All(text=>!text.text.Contains("ui_")&&!text.text.Contains("PropType."))),"Start refresh resolves original text and property objects");
        Require(ReferenceEquals(OfficialWwiseEvent.Host,previousSound) && ReferenceEquals(ProfessionPropertyBindings.Current,properties),"borrowed global bindings preserved");
        Capture("home-level-up-owner-duplicates.png");
        yield return new WaitForSeconds(1.9f);
        Require(owner.WindowCount==0 && canvas.GetComponentsInChildren<LevelUpNotifyTip>().Length==0,"both original 2.2-second auto-close transitions complete");
        Require(canvas.sortingOrder==40,"notification never rewrites parent canvas sorting");
        Require(audioHost.RegisteredObjectCount==0 && sounds==2,"original emitters released and both sounds posted");
        string serialized=JsonUtility.ToJson(player); string saved=Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),"owner-player.json");
        File.WriteAllText(saved,serialized); player=JsonUtility.FromJson<OfflinePlayerProgress>(File.ReadAllText(saved)); player.Level=3;
        Require(LevelUpNotifyTip.TryPopup(owner,2),"current player delegate reads reloaded model");
        Require(canvas.GetComponentInChildren<LevelUpNotifyTip>().GetComponent<Canvas>().sortingOrder==50,"empty container restores original sorting baseline");
        yield return new WaitForSeconds(.9f); Capture("home-level-up-owner-reloaded.png");
        owner.Dispose(); yield return null;
        Require(owner.WindowCount==0 && audioHost.RegisteredObjectCount==0,"disposing during animation releases duplicate tip objects");
        Require(calculator.Evaluate(3).Count>0,"borrowed calculator remains available");
        int showing=0,closed=0;
        bindings.Showing=popup=>{ showing++; popup.SetCanvasSortingOrder(110); };
        bindings.Closed=popup=>closed++;
        owner=new OfflineHomeLevelUpWindows(bindings,calculator,()=>player.Level);
        LevelUpNotifyTip.TryPopup(owner,2); yield return null;
        Require(canvas.GetComponentInChildren<LevelUpNotifyTip>().GetComponent<Canvas>().sortingOrder==110,"shared sorting owner receives original OnShowing");
        owner.Close(canvas.GetComponentInChildren<LevelUpNotifyTip>().gameObject); owner.Dispose(); yield return null;
        Require(audioHost.RegisteredObjectCount==0 && sounds==4 && showing==1 && closed==1,"fresh owner and explicit close release once through shared container");
        ProfessionPropertyBindings.Current=previousProperties;
    }
    void Capture(string name)
    {
        Canvas.ForceUpdateCanvases(); cameraUi.Render(); var previous=RenderTexture.active; RenderTexture.active=target;
        var image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
        image.ReadPixels(new Rect(0,0,target.width,target.height),0,0); image.Apply(); RenderTexture.active=previous;
        File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),name),image.EncodeToPNG()); Object.Destroy(image);
    }
    static void Require(bool value,string message) { if(!value) throw new Exception(message); }
}
