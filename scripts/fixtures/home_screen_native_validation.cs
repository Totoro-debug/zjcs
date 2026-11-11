using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Official.HomeScreenUI;
using Object=UnityEngine.Object;
using NativeSafeArea=Zjcs.Official.HomeScreenUI.SafeArea;

[InitializeOnLoad]
public static class HomeScreenNativeValidation
{
    const string Key="Zjcs.HomeScreenValidation.Active";
    static HomeScreenNativeValidation()
    {
        EditorApplication.playModeStateChanged+=state=>{
            if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key,false))
                new GameObject("OriginalHomeScreenValidation").AddComponent<HomeScreenNativeRunner>();
        };
        EditorApplication.update+=()=>{
            if(SessionState.GetBool(Key,false) && EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+".Deadline",float.MaxValue))
                Finish(new TimeoutException("Home screen validation timed out."));
        };
    }
    public static void Run()
    {
        var work=Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
        if(!Path.GetFullPath(Application.dataPath).StartsWith(work+Path.DirectorySeparatorChar))
            throw new InvalidOperationException("Owned isolated project required.");
        Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION","1");
        AudioListener.volume=0;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetBool(Key,true);SessionState.SetFloat(Key+".Deadline",(float)EditorApplication.timeSinceStartup+180);
        EditorApplication.EnterPlaymode();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool(Key,false);
        if(error!=null)Debug.LogException(error);
        else Debug.Log("HOME_SCREEN_NATIVE_PASSED: original native frame, screen-metric conversion, width/height clipping, dynamic border clones, safe-area padding/disable branches, resize and service lifecycle. Full production HUD remains separate.");
        EditorApplication.Exit(error==null?0:1);
    }
}

public sealed class HomeScreenNativeRunner:MonoBehaviour
{
    sealed class Metrics:IScreenMetrics
    {
        public int Width {get;set;}
        public int Height {get;set;}
        public ScreenOrientation Orientation {get;set;}
        public Rect SafeArea {get;set;}
    }
    readonly Metrics metrics=new Metrics();
    IDisposable binding;
    ScreenAdaptService service;
    GameObject frame,holder;
    RectTransform root,content,borders;
    CanvasWidthAdjuster adjuster;
    NativeSafeArea safe;
    Canvas canvas;
    Camera camera;
    RenderTexture target;
    int errors;
    string Work=>Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
    void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
    IEnumerator Start()
    {
        AudioListener.volume=0;Application.logMessageReceived+=Log;
        var steps=Exercise();
        for(;;){bool next;object current=null;try{next=steps.MoveNext();if(next)current=steps.Current;}
            catch(Exception error){HomeScreenNativeValidation.Finish(error);yield break;}
            if(!next)break;yield return current;}
        HomeScreenNativeValidation.Finish(null);
    }
    IEnumerator Exercise()
    {
        int nativeWidth=Screen.width,nativeHeight=Screen.height;
        binding=ScreenMetrics.Bind(metrics);
        service=new ScreenAdaptService();service.Initialize();
        metrics.Width=nativeWidth;metrics.Height=nativeHeight;metrics.SafeArea=new Rect(0,0,nativeWidth,nativeHeight);
        camera=new GameObject("ScreenFrameCamera",typeof(Camera)).GetComponent<Camera>();
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.2f,.28f);camera.orthographic=true;
        holder=new GameObject("InactiveInstantiationOwner");holder.SetActive(false);
        var prefab=OfficialHomeScreenAssetCatalog.LoadDefault().framePrefab;
        Require(prefab!=null,"original screen frame is installed");
        frame=Instantiate(prefab,holder.transform,false);
        Require(frame.transform.localScale==Vector3.zero,"original MainCanvas serialized scale retained");
        // UIUtils.ResetUI(All): original position, rotation, scale, activation order.
        frame.transform.localPosition=Vector3.zero;frame.transform.localRotation=Quaternion.identity;frame.transform.localScale=Vector3.one;
        frame.SetActive(true);
        root=(RectTransform)frame.transform;content=(RectTransform)root.Find("Content");borders=(RectTransform)root.Find("CanvasBorder");
        canvas=frame.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=10;
        adjuster=frame.GetComponent<CanvasWidthAdjuster>();adjuster.ScreenAdaptService=service;
        safe=content.Find("Main/Public").GetComponent<NativeSafeArea>();safe.ScreenAdaptService=service;
        Require(frame.GetComponentsInChildren<Transform>(true).Length==7,"explicit seven-node frame selection");
        Require(frame.GetComponentsInChildren<MonoBehaviour>(true).All(c=>c!=null),"no missing native components");
        Require(borders.GetComponentsInChildren<Image>(true).All(i=>i.sprite!=null && i.sprite.texture!=null),"original border Sprite/texture bindings");
        var scaler=frame.GetComponent<CanvasScaler>();
        Require(scaler.referenceResolution==new Vector2(1080,1920) && scaler.screenMatchMode==CanvasScaler.ScreenMatchMode.Expand,"original scaler parameters");
        SetTarget(nativeWidth,nativeHeight);holder.SetActive(true);
        yield return Settle();
        Require(service.CanvasWidthAdjusters.Single()==adjuster,"Start registers original adjuster");
        service.PauseShowBorder();Require(!borders.gameObject.activeSelf,"service pause hides border container");
        service.ResumeShowBorder();Require(borders.gameObject.activeSelf,"service resume restores border container");
        // Render targets and scoped metrics exercise layouts without OS resolution changes.
        SetTarget(800,600);metrics.SafeArea=new Rect(200,20,500,550);
        yield return Settle();
        Require(root.sizeDelta.x/root.sizeDelta.y>121f/160f,"wide render canvas uses the requested aspect");
        RectNear(Pixel(adjuster.BorderSafeArea),new Rect(200,0,426.875f,600),"wide border + asymmetric system inset");
        var result=adjuster.LastAdaptResult;
        Near(content.offsetMin.x,result.ClippedWidthLeft,"content left offset");
        Near(content.offsetMax.x,-result.ClippedWidthRight,"content right offset");
        Near(result.ClippedWidthLeft,adjuster.BorderSafeArea.xMin+2,"official two-UI-unit overlap");
        Require(borders.Find("LeftBorder").gameObject.activeSelf && borders.Find("RightBorder").gameObject.activeSelf,"wide side borders enabled");
        Require(!adjuster.Top.gameObject.activeSelf && !adjuster.Bottom.gameObject.activeSelf,"wide top/bottom borders hidden");
        RectNear(Pixel(safe.GetApplySafeArea()),new Rect(200,20,426.875f,550),"safe area intersects border");
        RectNear(NativeSafeArea.SystemSafeAreaWithWidthAdapt(safe.transform),new Rect(200,20,426.875f,550),"safe pixels for downstream callers");
        VerifySafeTransform();Capture("home-screen-wide.png");
        // Actual native graphic raycasting: a border consumes a point outside the content.
        var eventSystem=new GameObject("FrameInput",typeof(EventSystem)).GetComponent<EventSystem>();
        var hits=new System.Collections.Generic.List<RaycastResult>();
        var input=new PointerEventData(eventSystem){position=new Vector2(30,300),button=PointerEventData.InputButton.Left};
        eventSystem.RaycastAll(input,hits);
        Require(hits.Any(h=>h.gameObject.name=="LeftBorder"),"original border image participates in pointer hit testing");
        SetTarget(400,1200);metrics.SafeArea=new Rect(0,250,400,850);
        yield return Settle();
        Require(adjuster.LastAdaptResult.IsHeightAdapted,"tall aspect branch");
        RectNear(Pixel(adjuster.BorderSafeArea),new Rect(0,250,400,794.44446f),"tall border clips to system and ratio");
        Require(!borders.Find("LeftBorder").gameObject.activeSelf && !borders.Find("RightBorder").gameObject.activeSelf,"tall side borders hidden");
        Require(adjuster.Top.gameObject.activeSelf && adjuster.Bottom.gameObject.activeSelf,"tall dynamic borders enabled");
        Require(adjuster.Top.GetComponent<Image>().sprite==borders.Find("LeftBorder").GetComponent<Image>().sprite,"dynamic borders clone original Sprite");
        Near(content.offsetMin.y,adjuster.LastAdaptResult.ClippedHeightBottom,"content bottom offset");
        VerifySafeTransform();Capture("home-screen-tall.png");
        SetTarget(608,1080);metrics.SafeArea=new Rect(13,23,580,1000);
        yield return Settle();
        Require(!adjuster.LastAdaptResult.IsWidthAdapt && !adjuster.LastAdaptResult.IsHeightAdapted,"normal portrait ratio remains uncut");
        RectNear(Pixel(safe.GetApplySafeArea()),new Rect(13,23,580,1000),"portrait system safe area");
        safe.PaddingBottom=17;safe.PaddingTop=-19;safe.Dirty=true;yield return Settle();
        var padding=safe.GetApplySafeArea();Near(padding.yMin,23*root.rect.height/1080+17,"original bottom padding");
        Near(padding.yMax,1023*root.rect.height/1080-19,"original top padding sign");
        VerifySafeTransform();
        safe.DisableBottomAdapt=true;safe.DisableTopAdapt=true;safe.Dirty=true;yield return Settle();
        Near(safe.GetApplySafeArea().yMin,0,"disable bottom ignores padding and system inset");
        Near(safe.GetApplySafeArea().yMax,root.rect.height,"disable top ignores padding and system inset");
        VerifySafeTransform();Capture("home-screen-portrait.png");
        Require(borders.childCount==4,"dynamic border clones reused across resizes");
        Require(Screen.width==nativeWidth && Screen.height==nativeHeight,"validation leaves OS/editor screen metrics untouched");
        Destroy(holder);yield return null;Require(!service.CanvasWidthAdjusters.Any(),"OnDestroy unregisters before service disposal");
        service.Dispose();service=null;binding.Dispose();binding=null;
        Require(ScreenMetrics.width==Screen.width && ScreenMetrics.height==Screen.height,"native metric fallback restored");
        Require(errors==0,"no runtime rendering or lifecycle errors");
    }
    void SetTarget(int width,int height)
    {
        camera.targetTexture=null;
        if(target!=null){target.Release();Destroy(target);}
        target=new RenderTexture(width,height,24);target.Create();camera.targetTexture=target;
        metrics.Width=width;metrics.Height=height;metrics.Orientation=width>height?ScreenOrientation.LandscapeLeft:ScreenOrientation.Portrait;
        metrics.SafeArea=new Rect(0,0,width,height);
    }
    IEnumerator Settle(){yield return null;Canvas.ForceUpdateCanvases();yield return null;yield return null;}
    Rect Pixel(Rect rect)=>ScreenWidthAdapterUtils.ConvertUIRectToScreenPixelRect(rect,root);
    void VerifySafeTransform()
    {
        var corners=new Vector3[4];safe.GetComponent<RectTransform>().GetWorldCorners(corners);
        Vector2 origin=root.InverseTransformPoint(corners[0]);origin-=root.rect.min;
        var actual=new Rect(origin,safe.GetComponent<RectTransform>().rect.size);
        RectNear(actual,safe.GetApplySafeArea(),"actual safe-area transform");
    }
    void Capture(string name)
    {
        camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;
        var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();
        File.WriteAllBytes(Path.Combine(Work,name),image.EncodeToPNG());RenderTexture.active=previous;Destroy(image);
    }
    static void RectNear(Rect actual,Rect expected,string message)
    {Near(actual.x,expected.x,message+" x");Near(actual.y,expected.y,message+" y");Near(actual.width,expected.width,message+" width");Near(actual.height,expected.height,message+" height");}
    static void Near(float actual,float expected,string message){Require(Math.Abs(actual-expected)<.2f,message+": "+actual+" expected "+expected);}
    static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
}
