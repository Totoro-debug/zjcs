using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;
using Zjcs.Official.BattleRoundUI;
using Zjcs.Official.BattleRoundUI.DynamicTexture2DAtlas;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.Character;
using Zjcs.Presentation.CharacterCreation;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Zjcs.Editor.BattleRound
{
    public static class OfficialBattleRoundSceneAcceptance
    {
        private const string Key = "Zjcs.BattleRoundScene.Acceptance";
        private static bool started;
        private static int errors;
        public static void ExportPlayerAssets()
        {
            Environment.SetEnvironmentVariable("ZJCS_ROUND_EXPORT_PLAYER_ASSETS", "1");
            RunIsolated();
        }
        public static void Run()
        {
            Environment.SetEnvironmentVariable("ZJCS_CREATION_AUTOMATION", "1");
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
            Type.GetType("Zjcs.Editor.ProjectTools, Zjcs.Editor", true).GetMethod("Validate").Invoke(null, null);
            RunIsolated();
        }
        public static void RunIsolated()
        {
            Environment.SetEnvironmentVariable("ZJCS_CREATION_AUTOMATION", "1");
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
            Type.GetType("Zjcs.Editor.Home.HomeAcceptanceLauncher, Zjcs.Editor", true)
                .GetMethod("ConfigurePortraitGameView", System.Reflection.BindingFlags.Static
                    | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                .Invoke(null, new object[] { 450, 975 });
            SessionState.SetBool(Key, true);
            SessionState.SetInt(Key + ".result", 1);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 300);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Hook();
            EditorApplication.isPlaying = true;
        }
        [InitializeOnLoadMethod] private static void Hook()
        {
            if (!SessionState.GetBool(Key, false)) return;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= Mode;
            EditorApplication.playModeStateChanged += Mode;
        }
        private static void Mode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
            Application.logMessageReceived -= Observe;
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= Mode;
            EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
        }
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
            {
                Debug.LogError("Native round scene timed out.");
                EditorApplication.isPlaying = false;
                return;
            }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true;
            Application.logMessageReceived += Observe;
            new GameObject("Native round scene verification runner").AddComponent<Runner>().StartCoroutine(Verify());
        }
        private static void Observe(string message, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
        }
        private static IEnumerator Verify()
        {
            var stack = new Stack<IEnumerator>();
            Action<string> capture = name => ScreenCapture.CaptureScreenshot(
                Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), name + ".png"));
            stack.Push(Steps(capture));
            bool passed = false;
            try
            {
                while (stack.Count > 0)
                {
                    var current = stack.Peek();
                    if (!current.MoveNext())
                    {
                        (stack.Pop() as IDisposable)?.Dispose();
                        continue;
                    }
                    if (current.Current is IEnumerator nested) stack.Push(nested);
                    else yield return current.Current;
                }
                passed = errors == 0;
                if (passed && Environment.GetEnvironmentVariable("ZJCS_ROUND_EXPORT_PLAYER_ASSETS") == "1")
                {
                    string[] paths = Resources.FindObjectsOfTypeAll<UnityEngine.Object>()
                        .Select(AssetDatabase.GetAssetPath).Where(p => p.StartsWith("Assets/", StringComparison.Ordinal)
                            && !p.EndsWith(".cs", StringComparison.Ordinal)).Distinct().ToArray();
                    File.WriteAllLines(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),
                        "player-assets.txt"), AssetDatabase.GetDependencies(paths, true));
                }
            }
            finally
            {
                while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
                SessionState.SetInt(Key + ".result", passed ? 0 : 1);
                EditorApplication.isPlaying = false;
            }
        }
  private static void Require(bool ok,string reason) { if(!ok) throw new InvalidOperationException(reason); }
  private static IEnumerator Click(EventSystem events, Vector2 pos, Transform expected)
  {
   var data=new PointerEventData(events){position=pos,button=PointerEventData.InputButton.Left};
   var hits=new List<RaycastResult>();events.RaycastAll(data,hits);
   Require(hits.Count>0 && (hits[0].gameObject.transform==expected || hits[0].gameObject.transform.IsChildOf(expected)),"Original UI pointer is obscured: "+expected.name+" hit "+(hits.Count>0?hits[0].gameObject.name:"none"));
   var go=hits[0].gameObject;data.pointerCurrentRaycast=hits[0];data.pointerPressRaycast=hits[0];
   var pressed=ExecuteEvents.ExecuteHierarchy(go,data,ExecuteEvents.pointerDownHandler);
   if(pressed==null) pressed=ExecuteEvents.GetEventHandler<IPointerClickHandler>(go);
   data.pointerPress=pressed;data.rawPointerPress=go;
   yield return null;
   ExecuteEvents.Execute(pressed,data,ExecuteEvents.pointerUpHandler);
   ExecuteEvents.Execute(pressed,data,ExecuteEvents.pointerClickHandler);
   yield return null;
  }
  private static IEnumerator Steps(Action<string> capture)
  {
   var root = new GameObject("Native timeline verification");
   GameRoundScene scene = null;
   IDisposable renderPipelineScope = null;
   Texture2D maleHead = null, femaleHead = null;
   Sprite playerPortrait = null;
   IGridDynamicAtlas portraitAtlas = null;
   try
   {
    var portraitResources=Resources.Load<BattleRoundPortraitAssets>("Combat/RoundUI/PortraitAssets");
    Require(portraitResources!=null && portraitResources.Configuration.EnablePlayerPortraitETC2
     && portraitResources.Configuration.EnablePlayerPortraitETC2_Scene
     && portraitResources.Configuration.PreCreateEffectCount==20
     && portraitResources.Configuration.AutoStartWhenPlaybackBlackType.SequenceEqual(new[]{33})
     && portraitResources.Configuration.FactionEffectDatas.Count==2
     && portraitResources.Textures.Length==3 && portraitResources.Sprites.Length==3,
     "Complete native portrait configuration or explicit faction resources were lost.");
    var pipeline=Resources.Load<OfficialBattleRenderPipelineAssets>("Combat/OfficialBattleRenderPipelineAssets");
    Require(pipeline!=null,"Original battle render pipeline binding is missing.");
    renderPipelineScope=pipeline.Enter();
    yield return null;
    Require(GraphicsSettings.currentRenderPipeline==pipeline.Pipeline && QualitySettings.renderPipeline==null,
     "Timeline verification must use the same original render pipeline as the public battle controller.");
    Texture2D CaptureHead(int sex)
    {
     var settings=CharacterPortraitPhotography.LoadBattleHead();
     var model=CharacterCreationNativeModelFactory.CreateArenaGuardModel(sex,1,null,settings.excludes);
     try
     {
      model.transform.position=new Vector3(-1024,-1024,-1024);
      var animator=model.GetComponent<Animator>();
      Require(OfficialBattleCharacterAnimationCatalog.TryResolve(animator.runtimeAnimatorController,settings.animation,out string pose),"Original portrait pose missing.");
      return CharacterNativePortraitCapture.Capture(model,settings,pose);
     }
     finally {UnityEngine.Object.Destroy(model);}
    }
    maleHead=CaptureHead(0);femaleHead=CaptureHead(1);
    Require(maleHead.width==100 && maleHead.height==120 && femaleHead.width==100 && femaleHead.height==120
     && maleHead.GetPixels32().Count(c=>c.a>128)>1000 && femaleHead.GetPixels32().Count(c=>c.a>128)>1000,
     "Original male/female head capture is empty or incorrectly sized.");
    yield return OfficialBattlePortraitRequestAcceptance.Verify(maleHead,femaleHead);
    portraitAtlas=DynamicTexture2DAtlasUtils.CreateGridDynamicAtlas(1024,1024,new Vector2Int(100,120),null,true);
    Require((portraitAtlas is ETC2GridDynamicAtlas)==(DynamicTexture2DAtlasUtils.CanGraphicTextureCopy && DynamicTexture2DAtlasUtils.SupportETC2Fmt),"Original platform atlas selection changed.");
    Require(portraitAtlas is FallBackGridDynamicAtlas,"This Windows verification requires the actual unsupported-ETC2 fallback.");
    var maleInfo=portraitAtlas.Add(maleHead,101).SpriteInfo;
    var femaleInfo=portraitAtlas.Add(femaleHead,102).SpriteInfo;
    Require(maleInfo!=null && femaleInfo!=null && portraitAtlas.SpriteNum==2 && portraitAtlas.ContainsKey(101)
     && portraitAtlas.Get(101)==maleInfo && maleInfo.TexAtlas!=maleHead && femaleInfo.TexAtlas!=femaleHead
     && maleInfo.TexAtlas.GetPixels32().SequenceEqual(maleHead.GetPixels32())
     && femaleInfo.TexAtlas.GetPixels32().SequenceEqual(femaleHead.GetPixels32())
     && maleInfo.Sprite.pixelsPerUnit==100 && maleInfo.UVLB==Vector2.zero && maleInfo.UVRT==Vector2.one,
     "Fallback atlas changed original portrait pixels, UVs, scale, cache or texture ownership.");
    Texture2D removedTexture=maleInfo.TexAtlas;Sprite removedSprite=maleInfo.Sprite;
    Require(portraitAtlas.Remove(101) && !portraitAtlas.ContainsKey(101) && portraitAtlas.SpriteNum==1,"Fallback atlas removal failed.");
    Texture2D disposedTexture=femaleInfo.TexAtlas;Sprite disposedSprite=femaleInfo.Sprite;
    portraitAtlas.Dispose();portraitAtlas=null;
    yield return null;
    Require(removedTexture==null && removedSprite==null && disposedTexture==null && disposedSprite==null
     && maleHead!=null && femaleHead!=null,"Fallback atlas leaked owned assets or destroyed caller textures.");
    playerPortrait=Sprite.Create(maleHead,new Rect(0,0,100,120),new Vector2(.5f,.5f),100);
    var cameraObject = new GameObject("Battle camera", typeof(Camera));
    cameraObject.transform.SetParent(root.transform, false);
    cameraObject.transform.position = new Vector3(0, 0, -30);
    var camera = cameraObject.GetComponent<Camera>();
    camera.orthographic = true; camera.orthographicSize = 6;
    camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.15f,.24f,.28f);
    var canvasObject = new GameObject("Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
    canvasObject.transform.SetParent(root.transform,false);
    canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
    var scaler=canvasObject.GetComponent<CanvasScaler>();
    scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
    scaler.referenceResolution=new Vector2(1080,1920); scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
    var eventsObject=new GameObject("EventSystem",typeof(EventSystem)); eventsObject.transform.SetParent(root.transform,false);
    var events=eventsObject.GetComponent<EventSystem>();
    var hudCatalog=OfficialBattleHudCatalog.LoadDefault();
    Require(hudCatalog.Font.material.shader.isSupported,"Original HUD font shader is unsupported.");
    var hud=new OfficialBattleHudView(hudCatalog);
    GameObject hudRoot=hud.Mount(canvasObject.transform,camera);
    scene=UnityEngine.Object.FindObjectOfType<GameRoundScene>();
    Require(scene!=null && scene.IconRoot.name=="Slot" && scene.Line.name=="Linear", "Original scene component references were lost.");
    Require(scene.GetComponentsInChildren<Transform>(true).Length==4, "Native round scene hierarchy was reconstructed or extended.");
    Require(scene.Line.size==new Vector2(6.4f,.15f) && scene.Line.sortingOrder==-2, "Original round line geometry/order differs.");
    var roundUI=hudRoot.transform.Find("Content/Top/FixedContent/RoundUI").GetComponent<GameRoundUI>();
    Require(roundUI!=null && roundUI.IconInitExtendCount==16 && roundUI.MinDist==.05f
     && roundUI.IconMoveDuration==.2f && roundUI.IconPrefab!=null && roundUI.IconPrefabScene!=null
     && roundUI.IconBack.name=="Linear" && roundUI.IconSlot.name=="Slot",
     "Complete native manager subtree/references/serialized parameters were lost.");
    var nativeUGUI=roundUI.IconPrefab.GetComponent<GameRoundCharacterItem>();
    Require(nativeUGUI!=null && nativeUGUI.GetComponentsInChildren<MonoBehaviour>(true).Length==37
     && nativeUGUI.FactionBgImg!=null && nativeUGUI.MonsterIconImg!=null && nativeUGUI.MonsterHurtImg!=null
     && nativeUGUI.NpcIconImg!=null && nativeUGUI.PlayerPortrait!=null && nativeUGUI.Animator!=null,
     "Complete native UGUI card script/field closure is missing.");
    var originalMesh=nativeUGUI.GetComponentInChildren<UIMesh>(true).Mesh;
    Require(originalMesh.vertexCount==4 && originalMesh.GetVertexBufferStride(0)==20
     && originalMesh.triangles.SequenceEqual(new[]{0,1,2,0,2,3})
     && originalMesh.vertices.SequenceEqual(new[]{new Vector3(1.27f,-1.27f,0),new Vector3(1.27f,1.27f,0),
       new Vector3(-1.27f,1.27f,0),new Vector3(-1.27f,-1.27f,0)})
     && originalMesh.uv.SequenceEqual(new[]{new Vector2(0,0),new Vector2(0,1),new Vector2(1,1),new Vector2(1,0)}),
     "Original UGUI plane mesh bytes failed native deserialization.");
    foreach(var renderer in scene.GetComponentsInChildren<SpriteRenderer>())
     Require(renderer.sprite!=null && renderer.sharedMaterial!=null && renderer.sharedMaterial.shader.isSupported,"Native timeline sprite/material failed: "+renderer.name);
    var state=new FightBattleCheckpoint();
    for(int id=1;id<=3;id++)
    {
     var unit=new FightBattleUnitState { Id=id, CharacterType=id==1?1:0, NpcClassId=new[]{0,8411,11001}[id-1], Side=1, Hp=1, MaxHp=1 };
     unit.Turn.Id=id;unit.Turn.Hp=1;unit.Turn.Speed=100;unit.Turn.IntervalTicks=10000;
     state.Units.Add(unit);state.Timeline.Entries.Add(new FightTurnEntry {Id=id,Next=(id-1)*2500});
    }
    Func<FightBattleUnitState,Sprite> portrait=u=>u.Id==1?playerPortrait:OfficialBattleNpcPortraitCatalog.Load().Find(u.NpcClassId);
    var unqueued=new FightBattleUnitState {Id=99,Hp=1,MaxHp=1};
    state.Units.Add(unqueued);
    hud.RefreshTimeline(state,unit=>unit.Id==99?throw new InvalidOperationException("Unqueued entity requested a portrait."):portrait(unit),null,false);
    state.Units.Remove(unqueued);
    hud.RefreshPhase(FightBattlePhase.Preparing,new OfficialBattleSessionOptions());
    Require(!scene.GetComponent<OfficialBattleSceneTimelineView>().IsPlaying,
     "Round manager's original play-only gate is open during preparation.");
    hud.RefreshPhase(FightBattlePhase.Play,new OfficialBattleSessionOptions());
    Require(scene.GetComponent<OfficialBattleSceneTimelineView>().IsPlaying,
     "Round manager's original play-only gate did not follow the public HUD phase.");
    yield return new WaitForSeconds(.8f);
    Require(scene.GetComponentsInChildren<GameRoundCharacterItemScene>(true).Length==16,
     "Original manager did not precreate its serialized sixteen scene cards.");
    var anchor=(RectTransform)hudRoot.transform.Find(OfficialBattleTimelineAssets.Load().Anchor);
    void CheckProjection()
    {
     Canvas.ForceUpdateCanvases();
     var screen=RectTransformUtility.WorldToScreenPoint(null,anchor.position);
     Vector3 actual=camera.WorldToScreenPoint(scene.transform.position);
     Require(Vector2.Distance(screen,actual)<.1f && Mathf.Abs(scene.transform.position.z+16)<.00001f,"Native round scene lost its animated HUD anchor: expected="+screen+" actual="+actual+" world="+scene.transform.position);
     float expected=camera.orthographicSize/5*(scaler.referenceResolution.y/((RectTransform)canvasObject.transform).rect.height);
     Require(Mathf.Abs(scene.transform.localScale.x-expected)<.00001f,"Native round scene scale differs.");
    }
    void CheckActor(int id)
    {
     Transform card=scene.IconRoot.Find("GameRoundCharacterItem_"+id);
     float half=scene.Line.size.x*scene.Line.transform.localScale.x/2;
     Require(card!=null && Mathf.Abs(card.localPosition.x-half)<.00001f && card.GetSiblingIndex()==scene.IconRoot.childCount-1
      && card.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName("UIGameRound_Acting"),"Current actor order/animation differs: "+id+" pos="+(card==null?"missing":card.localPosition.ToString())+" expectedX="+half+" sibling="+(card==null?-1:card.GetSiblingIndex())+" count="+scene.IconRoot.childCount+" state="+(card==null?0:card.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).shortNameHash));
    }
    yield return null;CheckProjection(); CheckActor(1);
    var playerCircle=scene.IconRoot.Find("GameRoundCharacterItem_1/"+OfficialBattleTimelineAssets.Load().CardBindings.player).GetComponent<WorldCircleSpriteRenderer>();
    Texture2D timelineTexture=playerCircle.Sprite.texture;
    Require(timelineTexture!=maleHead && timelineTexture.GetPixels32().SequenceEqual(maleHead.GetPixels32()),"Public timeline did not use the original atlas fallback for a captured player head.");
    foreach(Transform card in scene.IconRoot)
    {
     var native=card.GetComponent<GameRoundCharacterItemScene>();
     Require(native!=null && native.RoundRuntimeData!=null && native.IsCurrent==(card.name=="GameRoundCharacterItem_1")
      && card.GetComponentsInChildren<MonoBehaviour>(true).Length==7
      && native.PlayerPortrait==card.GetComponentInChildren<GameRoundPlayerPortrait>(true)
      && native.DeadTime==.5f,"Complete original card scripts, fields or initial data were lost.");
     var group=card.GetComponent<SortingGroup>();
     Require(group!=null && group.sortingLayerID==scene.Line.sortingLayerID
      && group.sortingOrder==1000+card.GetSiblingIndex() && group.sortingOrder>scene.Line.sortingOrder
      && card.GetComponent<GameSceneObjectOrderLayer>()!=null,"Original card sorting was lost; timeline line can cover the portrait.");
    }
    capture("round-scene-initial");yield return null;
    state.Timeline.Acting=3;state.Timeline.Current=5000;
    state.Timeline.Entries[0].Next=10000;state.Timeline.Entries[1].Next=12500;state.Timeline.Entries[2].Next=20000;
    hud.RefreshTimeline(state,portrait,null,true);yield return new WaitForSeconds(.4f);
    CheckActor(3);Require(state.Timeline.Entries[0].Id==1 && state.Timeline.Entries[2].Next==20000,"Presentation modified authoritative timeline.");
    Require(scene.IconRoot.Find("GameRoundCharacterItem_3").GetComponent<GameRoundCharacterItemScene>().IsCurrent
     && !scene.IconRoot.Find("GameRoundCharacterItem_1").GetComponent<GameRoundCharacterItemScene>().IsCurrent,
     "Original card Refresh did not replace its current-actor flag.");
    capture("round-scene-acting");yield return null;
    var restored=JsonUtility.FromJson<FightBattleCheckpoint>(JsonUtility.ToJson(state));
    hud.RefreshTimeline(restored,portrait,null,false);yield return new WaitForSeconds(.4f);CheckActor(3);
    var driver=scene.GetComponent<OfficialBattleSceneTimelineView>().Driver;
    Require(driver.FindData(3).NextTime==5000 && restored.Timeline.Entries[2].Next==20000,
     "Restoring a checkpoint displayed the actor's future turn or mutated the checkpoint.");
    restored.Timeline.Acting=2;restored.Timeline.Current=7500;
    hud.RefreshTimeline(restored,portrait,null,true);yield return new WaitForSeconds(.4f);CheckActor(2);
    Require(driver.FindData(3).NextTime==20000 && driver.FindData(2).NextTime==7500,
     "Changing actors did not restore the previous actor's next-turn display time.");
    Require(playerCircle.Sprite.texture==timelineTexture,"Refreshing/restoring the public timeline duplicated the cached portrait texture.");
    camera.orthographicSize=8;yield return null;yield return null;CheckProjection();
    scaler.referenceResolution=new Vector2(1080,2160);Canvas.ForceUpdateCanvases();yield return null;yield return null;CheckProjection();
    var animation=hudRoot.GetComponent<OfficialBattleHudAnimationPlayer>();animation.enabled=false;
    Require(animation.Play("/FightMainPanel","FightMainPanel_Show"),"Original main show animation missing.");
    animation.Tick(.325f);Canvas.ForceUpdateCanvases();yield return null;yield return null;CheckProjection();
    capture("round-scene-anchor-animation");yield return null;
    animation.Tick(1);animation.enabled=true;
    scene.SetVisible(false);yield return null;
    Require(scene.transform.position==new Vector3(0,1000,0),"Hidden native scene was projected back on screen.");
    scene.SetVisible(true);yield return null;yield return null;CheckProjection();
    var alpha=scene.GetComponent<SpriteRendererCanvas>();Require(alpha!=null,"Original scene alpha component missing.");
    var colors=scene.GetComponentsInChildren<SpriteRenderer>().Select(r=>r.color).ToArray();
    alpha.Alpha=.4f;alpha.RefreshAlpha();
    Require(scene.GetComponentsInChildren<SpriteRenderer>().Select(r=>r.color).SequenceEqual(colors),"Original empty alpha references were populated by the host.");
    alpha.Alpha=1;alpha.RefreshAlpha();
    int speedClicks=0;hud.SpeedCycled+=()=>speedClicks++;
    Transform speed=hudRoot.transform.Find("FightMainPanelSpeedUI/Btn");
    yield return Click(events,RectTransformUtility.WorldToScreenPoint(null,((RectTransform)speed).TransformPoint(((RectTransform)speed).rect.center)),speed);
    Require(speedClicks==1,"Native scene blocked the original HUD speed pointer.");
    var hurt=new FightBattleStepEvent();
    hurt.DamageEvents.Add(new FightBattleDamageEvent {TargetId=1,Damage=-1,HitEntityId=1,DamageType=1});
    hud.RefreshTimeline(restored,portrait,null,true,hurt);yield return new WaitForSeconds(.05f);
    Require(scene.IconRoot.Find("GameRoundCharacterItem_1").GetComponent<Animator>()
     .GetCurrentAnimatorStateInfo(0).IsName("UIGameRound_Hurt"),"Original manager did not consume the public damage notification.");
    yield return new WaitForSeconds(.4f);
    restored.Units[1].Hp=0;hud.RefreshTimeline(restored,portrait,null,true);yield return new WaitForSeconds(.8f);
    Require(scene.IconRoot.Find("GameRoundCharacterItem_2")==null,"Dead actor card was retained.");
    capture("round-scene-after-death");yield return null;
    scene.SetVisible(false);
    var view=scene.GetComponent<OfficialBattleSceneTimelineView>();
    var inactive=new GameObject("UGUI card injection");inactive.SetActive(false);inactive.transform.SetParent(root.transform,false);
    var uiConfig=UnityEngine.Object.Instantiate(portraitResources.Configuration);
    uiConfig.EnablePlayerPortraitETC2_Scene=false;
    var uiAtlas=new FightDynamicTextureETC2();uiAtlas.Initialize(uiConfig,portraitResources.LoadTexture);
    bool slotWasActive=roundUI.IconSlot.gameObject.activeSelf;
    roundUI.IconSlot.gameObject.SetActive(true);
    try
    {
     foreach(bool etc2 in new[]{true,false})
     {
      uiConfig.EnablePlayerPortraitETC2=etc2;
      var cardRoot=UnityEngine.Object.Instantiate(roundUI.IconPrefab,inactive.transform,false);
      foreach(var selector in cardRoot.GetComponentsInChildren<GameRoundPlayerPortrait>(true))selector.BindConfiguration(uiConfig);
      foreach(var head in cardRoot.GetComponentsInChildren<HeadCharacterPortraitETC2>(true))head.BindAtlas(uiAtlas);
      foreach(var handler in cardRoot.GetComponentsInChildren<GameRoundPortraitETC2Handler>(true))handler.BindAtlas(uiAtlas);
      cardRoot.transform.SetParent(roundUI.IconSlot,false);cardRoot.SetActive(true);
      var card=cardRoot.GetComponent<GameRoundCharacterItem>();view.PrepareCard(card,1);
      card.Init(new BattleRoundData{EntityId=1,Interval=10000},true,view.GetRole(1));card.SetActing();
      yield return new WaitForSeconds(.3f);
      var images=cardRoot.GetComponentsInChildren<RawImage>(false);
      Require(images.Any(image=>image.texture!=null && image.texture.width==100 && image.texture.height==120),
       "Complete native UGUI card did not display the original head photograph: ETC2="+etc2);
      Require(images.Any(image=>image.uvRect==new Rect(0,0,1,1)
       && image.rectTransform.rect.size==new Vector2(100,120)),
       "Original full UV rectangle/native head dimensions failed to deserialize.");
      Require(card.MainPlayerEffect.activeSelf && card.IsCurrent && card.RoundRuntimeData!=null,
       "Original UGUI card data/main-player effect missing.");
      capture(etc2?"round-card-ugui-atlas":"round-card-ugui-normal");yield return null;
      cardRoot.SetActive(false);UnityEngine.Object.Destroy(cardRoot);yield return null;
     }
    }
    finally {roundUI.IconSlot.gameObject.SetActive(slotWasActive);uiAtlas.Dispose();UnityEngine.Object.Destroy(uiConfig);UnityEngine.Object.Destroy(inactive);scene.SetVisible(true);}
    restored.Timeline.Entries.Clear();hud.RefreshTimeline(restored,portrait,null,false);yield return null;
    Require(scene.IconRoot.childCount==0,"Empty timeline retained cards.");
    Require(timelineTexture!=null,"Portrait atlas was released before the battle scene scope ended.");
    UnityEngine.Object.Destroy(hudRoot);yield return null;yield return null;
    Require(scene==null && timelineTexture==null && maleHead!=null,"HUD lifetime leaked the native round scene/portrait atlas or destroyed the caller capture.");
    Debug.Log("Native portrait atlas passed: actual Windows fallback selection, original male/female capture pixels, UV/scale, cache identity, removal/disposal ownership, public timeline reuse and scene lifetime.");
    Debug.Log("Native round scene passed: original references/materials/card sorting, public HUD, actor order/restore/death, camera/canvas/animated-anchor projection, hide/show, original empty alpha references, simulated speed pointer, empty queue and lifetime.");
   }
   finally {portraitAtlas?.Dispose();UnityEngine.Object.Destroy(root);if(scene!=null)UnityEngine.Object.Destroy(scene.gameObject);UnityEngine.Object.Destroy(playerPortrait);UnityEngine.Object.Destroy(maleHead);UnityEngine.Object.Destroy(femaleHead);renderPipelineScope?.Dispose();}
   yield return null;
  }
        private sealed class Runner : MonoBehaviour { }
    }
}
