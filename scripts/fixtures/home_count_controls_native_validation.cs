using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Official.HomeCountControlsUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Offline.HomeHudUI;
using Zjcs.Offline.PartnersUI;
using Object=UnityEngine.Object;
[InitializeOnLoad]
public static class HomeCountControlsNativeValidation
{
    const string Key="Zjcs.HomeCountControlsNativeValidation";
    static HomeCountControlsNativeValidation()
    {
        EditorApplication.playModeStateChanged+=state=>{
            if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false))new GameObject("CompleteHomeUiBindingFixture").AddComponent<HomeCountControlsNativeRunner>();
        };
        EditorApplication.update+=()=>{if(SessionState.GetBool(Key,false)&&EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+".Deadline",float.MaxValue))Finish(new TimeoutException("Full HomeUI binding fixture timed out."));};
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
        else Debug.Log("HOME_COUNT_CONTROLS_NATIVE_PASSED: original quick-buy quantity controls, simulated pointer clicks/drags/holds, bounds, acceleration/cancellation and disposal. Isolated original-size control slots; complete currency windows and production integration remain pending.");
        EditorApplication.Exit(error==null?0:1);
    }
}
public sealed class HomeCountControlsNativeRunner:MonoBehaviour
{
    Camera cameraUi;RenderTexture target;EventSystem events;Dictionary<string,string> texts;Vector2 pointer;int errors,sounds;
    void OnLog(string text,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
    IEnumerator Start()
    {
        AudioListener.volume=0;Application.logMessageReceived+=OnLog;var steps=Exercise();
        for(;;){bool next;object current=null;try{next=steps.MoveNext();if(next)current=steps.Current;}catch(Exception e){HomeCountControlsNativeValidation.Finish(e);yield break;}if(!next)break;yield return current;}
        HomeCountControlsNativeValidation.Finish(null);
    }
    IEnumerator Exercise()
    {
        texts=JsonUtility.FromJson<LanguageData>(AssetDatabase.LoadAssetAtPath<TextAsset>("@TEXT_PATH@").text).texts.ToDictionary(x=>x.key,x=>x.text);
        LanguageManager.Initialize(new Language(texts));
        cameraUi=new GameObject("MutedQuantityCamera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();cameraUi.orthographic=true;cameraUi.clearFlags=CameraClearFlags.SolidColor;cameraUi.backgroundColor=new Color(.12f,.2f,.28f);
        target=new RenderTexture(Screen.width,Screen.height,24);target.Create();cameraUi.targetTexture=target;
        var canvasObject=new GameObject("ControlFixtureCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));var canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=cameraUi;canvas.planeDistance=1;
        var scaler=canvasObject.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1080,1920);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
        events=new GameObject("InternalPointer",typeof(EventSystem)).GetComponent<EventSystem>();
        var slot=new GameObject("Original798x162QuantitySlot",typeof(RectTransform));var rect=(RectTransform)slot.transform;rect.SetParent(canvas.transform,false);rect.sizeDelta=new Vector2(798,162);
        var staging=new GameObject("InactiveDependencyBinding");staging.SetActive(false);
        var catalog=OfflineHomeHudAssets.LoadDefault();var soundScope=new OfflinePartnerUiSoundScope(catalog.Sound);var fonts=catalog.CreateFontService();fonts.Initialize();var clip=catalog.Sound(@SOUND_ID@u);Require(clip,"original PCM bound");
        GameObject Instantiate(string path)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);Require(prefab,"native quantity prefab exists");var go=Object.Instantiate(prefab,staging.transform,false);
            Require(go.GetComponentsInChildren<MonoBehaviour>(true).All(x=>x),"all native control script bindings resolve");
            foreach(var text in go.GetComponentsInChildren<TMP_Text>(true))if(text.font)fonts.SetupFont(text.font);
            foreach(var text in go.GetComponentsInChildren<WorldMultiLanguageText>(true))text.Localizer=key=>texts.TryGetValue(key,out var value)?value:key;
            foreach(var button in go.GetComponentsInChildren<WorldButtonSound>(true))button.Post=(audio,emitter)=>{Require(audio==clip,"original button PCM");sounds++;var player=emitter.GetComponent<AudioSource>();if(!player)player=emitter.AddComponent<AudioSource>();player.volume=0;player.PlayOneShot(audio);};
            foreach(var control in go.GetComponentsInChildren<SetCountSlider>(true))control.GlobalColors=new Colors();
            foreach(var touch in go.GetComponentsInChildren<LongTouch>(true))touch.PointerPositionOverride=()=>pointer;
            go.transform.SetParent(slot.transform,false);go.SetActive(false);return go;
        }
        var sliderObject=Instantiate("@COUNT_SLIDER_PREFAB@");sliderObject.SetActive(true);yield return null;
        var control=sliderObject.GetComponent<SetCountSlider>();Require(control&&control.Slider.wholeNumbers,"original Awake sets integer slider");
        var updates=new List<long>();control.OnCurrentCountUpdate+=updates.Add;control.Init(1,10,3,2);Require(control.CurrentCount==3&&updates.SequenceEqual(new long[]{3}),"Init reports original value once");
        Click(control.AddBtn);Require(control.CurrentCount==4,"native add pointer");Click(control.MinusBtn);Require(control.CurrentCount==3,"native minus pointer");
        Require(control.CurrentCountText.Text.Contains("6/20"),"original amount-per-trade text formatting");Capture("home-count-slider-native.png");
        Click(control.MaxBtn);Require(control.CurrentCount==10,"native maximum pointer");Click(control.AddBtn);Require(control.CurrentCount==10,"slider upper boundary");control.Init(1,10,3);
        var sliderRect=(RectTransform)control.Slider.transform;var area=control.Slider.handleRect.parent as RectTransform;var point=area.TransformPoint(new Vector3(area.rect.xMin+area.rect.width*.75f,area.rect.center.y));
        var drag=Pointer(RectTransformUtility.WorldToScreenPoint(cameraUi,point));var dragHits=new List<RaycastResult>();events.RaycastAll(drag,dragHits);Require(dragHits.Count>0&&ExecuteEvents.GetEventHandler<IDragHandler>(dragHits[0].gameObject)==control.Slider.gameObject,"native slider is first drag target");drag.pointerPressRaycast=dragHits[0];drag.pointerCurrentRaycast=dragHits[0];ExecuteEvents.Execute(control.Slider.gameObject,drag,ExecuteEvents.initializePotentialDrag);ExecuteEvents.Execute(control.Slider.gameObject,drag,ExecuteEvents.pointerDownHandler);ExecuteEvents.Execute(control.Slider.gameObject,drag,ExecuteEvents.beginDragHandler);ExecuteEvents.Execute(control.Slider.gameObject,drag,ExecuteEvents.dragHandler);ExecuteEvents.Execute(control.Slider.gameObject,drag,ExecuteEvents.endDragHandler);ExecuteEvents.Execute(control.Slider.gameObject,drag,ExecuteEvents.pointerUpHandler);
        Require(control.CurrentCount>=7&&control.CurrentCount<=9,"native slider simulated drag sets bounded integer value: "+control.CurrentCount);
        control.Init(1,1,99);Require(control.CurrentCount==1&&!control.Slider.interactable&&control.Slider.value==1,"single selectable value with disabled operation");Click(control.AddBtn);Require(control.CurrentCount==1,"disabled-range add leaves value");
        control.Init(5,2,1);Require(control.CurrentCount==0&&!control.Slider.interactable&&control.Slider.value==0,"reversed range selects zero and disables operation");Require(control.CurrentCountText.Text.Contains("0"),"original zero text branch");
        control.Init(1,10000,1);var longTouch=control.AddLongTouch;Require(longTouch,"original add LongTouch reference");var heldValues=new List<long>();longTouch.OnLongTouch+=heldValues.Add;
        var down=Down(control.AddBtn);var deadline=Time.time+10;
        while(!heldValues.Any(x=>x>1)){Require(Time.time<deadline,"original hold accelerates within deadline");yield return null;}
        Require(heldValues.Count>=11&&heldValues.Take(11).All(x=>x==1)&&heldValues[11]==2,"original hold threshold and first acceleration step");
        ExecuteEvents.Execute(control.AddBtn.gameObject,down,ExecuteEvents.pointerUpHandler);int released=heldValues.Count;var until=Time.time+longTouch.InitInterval+.1f;while(Time.time<until)yield return null;Require(heldValues.Count==released,"pointer up stops original hold");
        Down(control.AddBtn);pointer+=new Vector2(events.pixelDragThreshold+1,0);yield return null;yield return null;until=Time.time+longTouch.InitInterval+.1f;while(Time.time<until)yield return null;Require(heldValues.Count==released,"movement beyond original drag threshold cancels hold");
        down=Down(control.AddBtn);down.dragging=true;yield return null;until=Time.time+longTouch.InitInterval+.1f;while(Time.time<until)yield return null;Require(heldValues.Count==released,"dragging event cancels hold");
        longTouch.AutoReset=true;Down(control.AddBtn);until=Time.time+longTouch.InitInterval*2+.2f;while(Time.time<until)yield return null;Require(heldValues.Count==released+1,"original AutoReset produces one held callback");
        sliderObject.SetActive(false);
        var groupObject=Instantiate("@COUNT_BUTTONS_PREFAB@");groupObject.SetActive(true);yield return null;
        var group=groupObject.GetComponent<SetCountButtonGroup>();int changed=0,exceeded=0;long current=1;group.Init(1,3,value=>{changed++;current=value;},1,value=>{Require(value==4,"original exceed callback proposed value");exceeded++;},true);
        var minus=Field<Button>(group,"mMinusBtn");var add=Field<Button>(group,"mAddBtn");var max=Field<Button>(group,"mMaxBtn");Click(minus);Require(changed==0,"group lower boundary");Click(add);Click(add);Require(current==3&&changed==2,"group add callback ordering");Click(add);Require(current==3&&changed==2&&exceeded==1,"group upper boundary uses exceed callback");Click(max);Require(changed==2,"max at same value does not notify");Click(minus);Click(max);Require(current==3&&changed==4,"group max pointer");Capture("home-count-buttons-native.png");
        Require(sounds>0,"original sound events run muted");Object.Destroy(sliderObject);Object.Destroy(groupObject);Object.Destroy(staging);yield return null;fonts.Dispose();soundScope.Dispose();Require(errors==0,"quantity controls lifecycle has no Unity errors");
    }
    PointerEventData Pointer(Vector2 point){pointer=point;return new PointerEventData(events){position=point,pointerId=-1,button=PointerEventData.InputButton.Left};}
    PointerEventData Down(Button button)
    {
        Require(button&&button.isActiveAndEnabled&&button.interactable,"native enabled button");Canvas.ForceUpdateCanvases();var rect=(RectTransform)button.transform;var data=Pointer(RectTransformUtility.WorldToScreenPoint(cameraUi,rect.TransformPoint(rect.rect.center)));var hits=new List<RaycastResult>();events.RaycastAll(data,hits);Require(hits.Count>0&&ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)==button.gameObject,"native first pointer target: "+button.name);ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerDownHandler);return data;
    }
    void Click(Button button){var data=Down(button);ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerUpHandler);ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerClickHandler);}
    void Capture(string name){Canvas.ForceUpdateCanvases();cameraUi.Render();var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),name),image.EncodeToPNG());RenderTexture.active=previous;Object.Destroy(image);}
    static T Field<T>(object value,string name)=>(T)value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(value);
    static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    sealed class Colors:IHomeCountColors{public Color LightRed=>new Color(.9254902f,.3372549f,.3372549f,1);public string WrapColor(string text,Color color)=>"<color=#"+ColorUtility.ToHtmlStringRGBA(color)+">"+text+"</color>";}
    [Serializable] sealed class LanguageData{public LanguageRow[] texts;}[Serializable] sealed class LanguageRow{public string key,text;}
    sealed class Language:ILocalizationTextService
    {
        readonly Dictionary<string,string> texts;public Language(Dictionary<string,string> values){texts=values;}
        string Text(string key)=>texts.TryGetValue(key,out var value)?value:key;
        public LanguageType CurrentLanguage=>LanguageType.zh_CN;public void ReloadData(){}public bool ContainsKey(string key)=>texts.ContainsKey(key);
        public bool TryGetOriginalText(string key,LanguageType language,out string result)=>texts.TryGetValue(key,out result);
        public string GetText(string key,LanguageType language,params object[] args)=>args.Length==0?Text(key):string.Format(Text(key),args);
        public string GetTextFormated(string key,LanguageType language,params object[] args)=>GetText(key,language,args);
        public void SetReplacer(ITextReplacer replacer){}public bool TryGetText(string key,LanguageType language,out string result)=>TryGetOriginalText(key,language,out result);
        public bool TryGetText(string key,out string result)=>texts.TryGetValue(key,out result);public void AddDataProvider(ILanguageTextDataProvider provider){}
    }
}
