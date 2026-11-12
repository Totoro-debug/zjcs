using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.HomeScreenUI;
using Object=UnityEngine.Object;

// Asset/ancestor and stencil validation only. The full HomeUI remains inactive
// until its production task, activity, result and scene services are supplied.
[InitializeOnLoad]
public static class HomeHudFrameNativeValidation
{
    const string Key="Zjcs.HomeHudFrameNativeValidation";
    static HomeHudFrameNativeValidation()
    {
        EditorApplication.playModeStateChanged+=state=>{
            if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false))
                new GameObject("OriginalHudFrameFixture").AddComponent<HomeHudFrameNativeRunner>();
        };
        EditorApplication.update+=()=>{if(SessionState.GetBool(Key,false)&&EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+".Deadline",float.MaxValue))Finish(new TimeoutException("Home shared frame fixture timed out."));};
    }
    public static void Run()
    {
        var work=Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
        if(!Path.GetFullPath(Application.dataPath).StartsWith(work+Path.DirectorySeparatorChar))throw new InvalidOperationException("Owned isolated project required.");
        Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION","1");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetBool(Key,true);SessionState.SetFloat(Key+".Deadline",(float)EditorApplication.timeSinceStartup+180);EditorApplication.EnterPlaymode();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool(Key,false);if(error!=null)Debug.LogException(error);
        else Debug.Log("HOME_HUD_FRAME_NATIVE_PASSED: original shared ancestors and stencil material/copy/order/visibility/color lifecycle; complete HomeUI imported with every script bound but held inactive. Full production HUD behavior remains unverified.");
        EditorApplication.Exit(error==null?0:1);
    }
}
public sealed class HomeHudFrameNativeRunner:MonoBehaviour
{
    int errors;Camera camera;RenderTexture target;
    IEnumerator Start()
    {
        AudioListener.volume=0;Application.logMessageReceived+=OnLog;
        var steps=Exercise();for(;;){bool next;object current=null;try{next=steps.MoveNext();if(next)current=steps.Current;}catch(Exception e){HomeHudFrameNativeValidation.Finish(e);yield break;}if(!next)break;yield return current;}
        HomeHudFrameNativeValidation.Finish(null);
    }
    void OnLog(string text,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
    IEnumerator Exercise()
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Official/Home/UI/HudFrame/HomeHudFrame.prefab");
        var hudPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Official/Home/UI/Hud/HomeHud.prefab");
        Require(prefab&&hudPrefab,"original frame and HomeUI prefabs imported");
        Require(hudPrefab.GetComponentsInChildren<MonoBehaviour>(true).All(x=>x),"all retained HomeUI scripts bound");
        Require(hudPrefab.GetComponentsInChildren<Transform>(true).All(x=>x.name!="TeamMiniPanel"&&x.name!="ChatMiniPanel"&&x.name!="ApplyConfirmBtn"&&x.name!="FunctionEntryChatQuick"),"only confirmed network subtrees removed");
        camera=new GameObject("MutedUiCamera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.2f,.28f);
        target=new RenderTexture(608,1080,24);target.Create();camera.targetTexture=target;
        var metrics=ScreenMetrics.Bind(new Metrics());var screen=new ScreenAdaptService();screen.Initialize();
        var inactive=new GameObject("InactiveOriginalFrame");inactive.SetActive(false);
        var frame=Object.Instantiate(prefab,inactive.transform,false);frame.SetActive(true);
        var rootCanvas=frame.GetComponent<Canvas>();rootCanvas.renderMode=RenderMode.ScreenSpaceCamera;rootCanvas.worldCamera=camera;rootCanvas.planeDistance=1;
        foreach(var canvas in frame.GetComponentsInChildren<Canvas>(true))canvas.worldCamera=camera;
        frame.GetComponent<CanvasWidthAdjuster>().ScreenAdaptService=screen;
        foreach(var safe in frame.GetComponentsInChildren<SafeArea>(true))safe.ScreenAdaptService=screen;
        Require(frame.transform.Find("Content/Main/Anim")&&frame.transform.Find("Content/Main/Public/Anim"),"both original feature/public animation ancestors retained");
        // Check the complete assembled prefab without running unbound services.
        var hud=Object.Instantiate(hudPrefab,frame.transform.Find("Content/Main/Anim"),false);hud.SetActive(false);
        Require(hud.GetComponent<Animator>()&&hud.GetComponent<Animator>().runtimeAnimatorController,"original HomeUI animator explicitly bound");
        var homeRect=(RectTransform)hud.transform;
        Require(homeRect.anchorMin==Vector2.zero&&homeRect.anchorMax==Vector2.one&&homeRect.sizeDelta==Vector2.zero,"original HomeUI full-screen anchors preserved");
        var stencil=frame.GetComponentInChildren<StencilMask>(true);Require(stencil&&stencil.StencilVal==6,"original stencil component and value retained");
        var image=stencil.GetComponent<Image>();var sourceColor=image.color;
        Require(!stencil.gameObject.activeSelf,"original player mask starts hidden");
        inactive.SetActive(true);stencil.SetVisible(true);yield return null;yield return null;
        var copy=Field<GameObject>(stencil,"mCopy");Require(copy&&copy.name=="Stencil"&&copy.transform.parent==stencil.transform.parent,"original mask duplicates under same public parent");
        Require(!copy.GetComponent<StencilMask>(),"copied controller destroyed before repeated Start");
        var material=copy.GetComponent<Image>().material;
        Require(material&&material.shader.name=="UI/Default"&&material.GetFloat("_StencilComp")==8&&material.GetFloat("_Stencil")==6&&material.GetFloat("_StencilOp")==2&&material.GetFloat("_ColorMask")==0,"original stencil shader and exact material values");
        Require(copy.GetComponent<Canvas>().overrideSorting&&copy.GetComponent<Canvas>().sortingOrder==-1,"original stencil renders before public content");
        Require(frame.GetComponentsInChildren<StencilMask>(true).Length==1,"no recursive duplicate stencil controllers");
        var duplicate=Field<GameObject>(stencil,"mCopy");stencil.SetColor(new Color(.2f,.7f,.3f,.6f));
        Require(Field<GameObject>(stencil,"mCopy")==duplicate&&image.color==new Color(.2f,.7f,.3f,.6f),"original repeated Init guard and SetColor target");
        Capture("home-hud-original-stencil.png");
        stencil.SetVisible(false);Require(!copy.activeSelf&&!stencil.gameObject.activeSelf,"original SetVisible hides source and duplicate");
        stencil.SetVisible(true);yield return null;Require(copy.activeSelf&&stencil.gameObject.activeSelf&&Field<GameObject>(stencil,"mCopy")==duplicate,"original visibility restore reuses copy");
        stencil.SetColor(sourceColor);Object.Destroy(inactive);yield return null;
        screen.Dispose();metrics.Dispose();Require(errors==0,"no Unity errors in actual frame/stencil Play Mode");
    }
    void Capture(string name){Canvas.ForceUpdateCanvases();camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(608,1080,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,608,1080),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),name),image.EncodeToPNG());RenderTexture.active=previous;Object.Destroy(image);}
    static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(owner);
    static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    sealed class Metrics:IScreenMetrics{public int Width=>608;public int Height=>1080;public ScreenOrientation Orientation=>ScreenOrientation.Portrait;public Rect SafeArea=>new Rect(0,0,608,1080);}
}
