using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Offline.World;
using Zjcs.Presentation.Character;
using Zjcs.Presentation.Combat;
using Zjcs.Presentation.World;

namespace Zjcs.Presentation.Home
{
    // Explicit Player diagnostic; all profile writes stay inside the supplied session directory.
    public sealed class SkillSummonPlayerValidation : MonoBehaviour
    {
        private string output;
        private Exception failure;
        private float deadline;
        private bool waitForStartup;
        [Serializable] private sealed class Result { public bool passed; public string error; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (Environment.GetEnvironmentVariable("ZJCS_SKILLS_VALIDATE_SUMMON_ONLY") != "1") return;
            var args=Environment.GetCommandLineArgs();
            int index=Array.IndexOf(args,"--zjcs-validate-skills");
            if(index<0 || index+1>=args.Length) return;
            StartDiagnostic(args[index+1]);
        }

        public static void StartDiagnostic(string output, bool waitForStartup = true)
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Run the skill diagnostic in Play Mode or Player.");
            var host=new GameObject("SkillSummonPlayerValidation");DontDestroyOnLoad(host);
            var driver=host.AddComponent<SkillSummonPlayerValidation>();
            driver.waitForStartup=waitForStartup;
            driver.output=Path.GetFullPath(output);Directory.CreateDirectory(driver.output);
            driver.deadline=Time.realtimeSinceStartup+1800f;Application.runInBackground=true;
            Application.logMessageReceived+=driver.ObserveLog;
            driver.StartCoroutine(driver.Guard(driver.Run()));
        }
        private void ObserveLog(string message,string stack,LogType type)
        {
            if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)
                failure=new InvalidOperationException(message+"\n"+stack);
        }
        private IEnumerator Guard(IEnumerator routine)
        {
            var stack=new Stack<IEnumerator>();stack.Push(routine);
            while(stack.Count>0)
            {
                object next;
                try
                {
                    Require(Time.realtimeSinceStartup<deadline,"Summon Player validation timed out.");
                    if(failure!=null) throw failure;
                    var current=stack.Peek();
                    if(!current.MoveNext()) {stack.Pop();continue;}
                    next=current.Current;
                    if(next is IEnumerator nested) {stack.Push(nested);continue;}
                }
                catch(Exception error)
                {
                    foreach (var controller in FindObjectsOfType<OfficialBattleRuntimeController>())
                        if (controller.Checkpoint != null)
                            File.WriteAllText(Path.Combine(output,"failure-checkpoint.json"),JsonUtility.ToJson(controller.Checkpoint,true));
                    File.WriteAllText(Path.Combine(output,"result.json"),JsonUtility.ToJson(new Result {error=error.ToString()},true));
                    Debug.LogException(error);Application.Quit(1);yield break;
                }
                yield return next;
            }
        }
        private IEnumerator Run()
        {
            while(waitForStartup && GameObject.Find("OfficialSaveSelectionPanel")==null) yield return null;
            if (SkillDarkStatusProcessValidation.ResumeRequested)
            {
                yield return SkillDarkStatusProcessValidation.Resume(output, CaptureGridFrame, ClickGridControl);
                yield break;
            }
            string saveRoot=Path.Combine(output,"save-"+Guid.NewGuid().ToString("N"));
            var slots=new ProfileSlotCatalog(saveRoot);
            string slot=slots.CreateSlot();
            var assets=OfficialCharacterAssetCatalog.LoadDefault();
            var save=new OfflineMainlineSave();
            save.Player.CharacterCreated=true;save.Player.CharacterName="召唤验证";
            save.Player.Level=40;save.Player.Rank=1;
            save.Player.SubRank=assets.Load().LevelLimits.Where(value=>value.PlayerLevel>=40
                && value.PlayerBlessLevel==0).Min(value=>value.SubRank);
            save.Player.InitialWeaponType=200;save.Player.Wardrobe.Profession=1;
            int initialSkill = 10014;
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ZJCS_FIRST_PROMOTION_SKILL")))
            {
                var source = OfficialBattleGraphCapabilityValidation.FirstPromotionSource();
                save.Player.InitialWeaponType = source.InitialWeaponType;
                save.Player.Wardrobe.Profession = source.Profession;
                initialSkill = source.InitialSkillItemId;
            }
            save.Player.Experience=assets.Load().CumulativeLevelExperience[40];
            save.Player.Skills.Skills.Add(new Zjcs.Offline.Skills.OfflineOwnedSkill {ClassId=initialSkill,Rank=1});
            File.WriteAllText(slots.GetProgressPath(slot),JsonUtility.ToJson(save,true));
            var session=OfflineMainlineProgressSession.OpenCharacter(slots,slot);
            var progression=new OfflineCharacterProgressionService(session,assets.Load());
            progression.InitializeWardrobe("skill-summon-player-wardrobe");
            progression.AttachSkillRules(assets.LoadSkillRules(),assets.LoadSkillRecommendations().recommendations);
            Require(progression.EquipSkill(initialSkill,0).IsApplied,"Could not equip the diagnostic's original initial skill.");
            foreach(var canvas in FindObjectsOfType<Canvas>()) canvas.gameObject.SetActive(false);
            foreach(var camera in FindObjectsOfType<Camera>()) camera.enabled=false;
            var home=HomeFeatureRuntime.Begin(slot,saveRoot,null);
            home.TutorialAutoStartEnabled=false;
            yield return null;
            if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_GRID_ACCEPTANCE") == "1")
            {
                yield return SkillGridBattleValidation.Exercise(home, slot, saveRoot, output,
                    ClickGridControl, CaptureGridFrame, value => home = value);
                File.WriteAllText(Path.Combine(output,"result.json"),JsonUtility.ToJson(new Result {passed=true},true));
                Destroy(home.gameObject);Application.Quit(0);yield break;
            }
            FightBattleCheckpoint settled=null;
            int sourceActor = 0;
            string sourceActorSetting = Environment.GetEnvironmentVariable("ZJCS_EMBEDDED_SOURCE_ACTOR");
            if (!string.IsNullOrEmpty(sourceActorSetting))
                Require(int.TryParse(sourceActorSetting, out sourceActor) && sourceActor > 0, "Invalid embedded source actor diagnostic.");
            OfficialBattleEmbeddedMonsterValidation embeddedProbe = null;
            bool checkAbsorption = Environment.GetEnvironmentVariable("ZJCS_STATUS_ABSORPTION_ACCEPTANCE") == "1";
            OfficialBattleStatusAbsorptionValidation absorptionProbe = null;
            string explicitMode = Environment.GetEnvironmentVariable("ZJCS_EXPLICIT_GRAPH_ACCEPTANCE");
            bool checkExplicitGraph = !string.IsNullOrEmpty(explicitMode) && explicitMode != "0";
            OfficialBattleExplicitGraphValidation explicitGraphProbe = null;
            string graphMode = Environment.GetEnvironmentVariable("ZJCS_GRAPH_CAPABILITY_ACCEPTANCE");
            bool checkGraphCapability = !string.IsNullOrEmpty(graphMode) && graphMode != "0";
            OfficialBattleGraphCapabilityValidation graphCapabilityProbe = null;
            bool checkHitFinish = Environment.GetEnvironmentVariable("ZJCS_HIT_FINISH_ACCEPTANCE") == "1";
            OfficialBattleHitFinishValidation hitFinishProbe = null;
            bool checkMasterChildren = Environment.GetEnvironmentVariable("ZJCS_MASTER_CHILD_ACCEPTANCE") == "1";
            OfficialBattleMasterChildValidation masterChildProbe = null;
            bool checkRandomHits = Environment.GetEnvironmentVariable("ZJCS_RANDOM_HIT_ACCEPTANCE") == "1";
            OfficialBattleRandomHitValidation randomHitProbe = null;
            bool checkStacks = Environment.GetEnvironmentVariable("ZJCS_SKILL_STACK_ACCEPTANCE") == "1";
            OfficialBattleSkillStackValidation stackProbe = null;
            bool checkCombo = Environment.GetEnvironmentVariable("ZJCS_PLAYER_COMBO_ACCEPTANCE") == "1";
            bool checkClones = Environment.GetEnvironmentVariable("ZJCS_PLAYER_CLONE_ACCEPTANCE") == "1";
            OfficialBattlePlayerCloneValidation cloneProbe = null;
            if (sourceActor != 0 || checkAbsorption || checkCombo || checkClones || checkStacks || checkRandomHits || checkMasterChildren || checkHitFinish || checkGraphCapability || checkExplicitGraph)
            {
                var weekly = OfflineWeeklyChallengeBattleCatalog.LoadDefault();
                Require(weekly.TryGetBattle(4800, out var stage, out var roster, out var scene), "Original weekly fixture is unavailable.");
                var checkpoint = OfficialMapBattleCheckpointBuilder.Create(new OfficialMapBattleCheckpointRequest {
                    Stage = stage, Roster = roster, Scene = scene, Player = home.Profile.Save.Player,
                    World = home.Profile.Save.World, Progression = home.DailyDungeonProgression, RandomSeed = 43 });
                Require(sourceActor == 0 || !checkAbsorption, "Select one original source diagnostic.");
                if (checkCombo || checkClones) cloneProbe = OfficialBattlePlayerCloneValidation.Configure(checkpoint);
                else if (checkExplicitGraph) explicitGraphProbe = OfficialBattleExplicitGraphValidation.Configure(checkpoint);
                else if (checkGraphCapability) graphCapabilityProbe = OfficialBattleGraphCapabilityValidation.Configure(checkpoint);
                else if (checkHitFinish) hitFinishProbe = OfficialBattleHitFinishValidation.Configure(checkpoint);
                else if (checkMasterChildren) masterChildProbe = OfficialBattleMasterChildValidation.Configure(checkpoint);
                else if (checkRandomHits) randomHitProbe = OfficialBattleRandomHitValidation.Configure(checkpoint);
                else if (checkStacks) stackProbe = OfficialBattleSkillStackValidation.Configure(checkpoint);
                else if (checkAbsorption) absorptionProbe = OfficialBattleStatusAbsorptionValidation.Configure(checkpoint);
                else embeddedProbe = OfficialBattleEmbeddedMonsterValidation.Configure(checkpoint, sourceActor);
                File.WriteAllText(Path.Combine(output,"source-actor-initial.json"),FightBattleCheckpointSerializer.Save(checkpoint));
                var weeklySession = new OfflineWeeklyChallengeSession(Path.Combine(saveRoot, "saves", slot, "weekly-challenge-save.json"));
                weeklySession.StartBattle(4800, weeklySession.CurrentPeriodStartLocalTicks(DateTime.Now), FightBattleCheckpointSerializer.Save(checkpoint));
            }
            var runner=home.StartWeeklyChallengeBattle(4800,cp=>settled=cp);
            var controller=runner.Controller;
            absorptionProbe?.BindPresentation(controller);
            stackProbe?.BindPresentation(controller);
            randomHitProbe?.BindPresentation(controller);
            masterChildProbe?.BindPresentation(controller);
            hitFinishProbe?.BindPresentation(controller);
            graphCapabilityProbe?.BindPresentation(controller);
            explicitGraphProbe?.BindPresentation(controller);
            bool checkDeath = Environment.GetEnvironmentVariable("ZJCS_PLAYER_DEATH_ACCEPTANCE") == "1";
            bool checkEmbedded = sourceActor != 0 || Environment.GetEnvironmentVariable("ZJCS_EMBEDDED_MONSTER_ACCEPTANCE") == "1";
            Require((checkClones ? 1 : 0) + (checkDeath ? 1 : 0) + (checkEmbedded ? 1 : 0)
                + (checkAbsorption ? 1 : 0) + (checkStacks ? 1 : 0) + (checkRandomHits ? 1 : 0) + (checkMasterChildren ? 1 : 0) + (checkHitFinish ? 1 : 0) + (checkGraphCapability ? 1 : 0) + (checkExplicitGraph ? 1 : 0) <= 1,
                "Select one isolated player diagnostic per battle.");
            cloneProbe ??= checkClones ? OfficialBattlePlayerCloneValidation.Configure(controller.Checkpoint) : null;
            var deathProbe = checkDeath ? OfficialBattlePlayerDeathValidation.Configure(controller.Checkpoint) : null;
            embeddedProbe ??= checkEmbedded ? OfficialBattleEmbeddedMonsterValidation.Configure(controller.Checkpoint) : null;
            var probe = checkClones || checkDeath || checkEmbedded || checkAbsorption || checkStacks || checkRandomHits || checkMasterChildren || checkHitFinish || checkGraphCapability || checkExplicitGraph ? null : OfficialSummonLifecycleValidation.Configure(controller.Checkpoint);
            controller.StepCompleted+=step=> {
                probe?.ObserveStep(controller); cloneProbe?.ObserveStep(controller); embeddedProbe?.ObserveStep(controller);
                absorptionProbe?.ObserveStep(controller);
                stackProbe?.ObserveStep(controller);
                randomHitProbe?.ObserveStep(controller);
                masterChildProbe?.ObserveStep(controller);
                hitFinishProbe?.ObserveStep(controller);
                graphCapabilityProbe?.ObserveStep(controller);
                explicitGraphProbe?.ObserveStep(controller);
                if (sourceActor != 0) File.WriteAllText(Path.Combine(output,"source-actor-last-step.json"),FightBattleCheckpointSerializer.Save(controller.Checkpoint));
            };
            Button start=null;
            float preparationDeadline=Time.realtimeSinceStartup+45f;
            while(start==null && Time.realtimeSinceStartup<preparationDeadline)
            {
                start=FindObjectsOfType<Button>().FirstOrDefault(value=>value.isActiveAndEnabled
                    && value.interactable && value.transform.parent.name=="StartBtn");
                if(start==null) yield return null;
            }
            Require(start!=null && controller.Phase==FightBattlePhase.Preparing,
                "Original battle start button is unavailable; phase="+controller.Phase);
            // Cold native asset loading must not spend the battle's own budget.
            deadline=Time.realtimeSinceStartup+(Application.isEditor ? 900f : 300f);
            yield return new WaitForEndOfFrame();Capture("summon-preparation");
            bool clicked=false;
            UnityEngine.Events.UnityAction observeClick=()=>clicked=true;
            start.onClick.AddListener(observeClick);
            try { yield return ClickGridControl(start); }
            finally { if(start!=null) start.onClick.RemoveListener(observeClick); }
            Require(clicked,"The native summon start button did not receive the simulated pointer click.");
            bool captured=false;
            bool resultCaptured=false;
            bool coldSceneRequested = Environment.GetEnvironmentVariable("ZJCS_STATUS_COLD_SCENE_ACCEPTANCE") == "1"
                || SkillDarkStatusProcessValidation.PrepareRequested;
            bool coldSceneCompleted = false;
            Require(!coldSceneRequested || graphCapabilityProbe != null,
                "Dark cold-scene validation requires the graph capability diagnostic.");
            while(runner!=null)
            {
                var nativeResult=FindObjectOfType<DemonResultWindow>();
                if(nativeResult && nativeResult.IsReady)
                {
                    Require(settled!=null && settled.Finished,"Native result opened before settlement.");
                    yield return new WaitForEndOfFrame();Capture("summon-result");
                    resultCaptured=true;
                    Click(nativeResult.Panel.GetComponent<Zjcs.Official.BattleResultUI.GameEndPanelDemonKing>().LeaveGameBtn);
                    yield return null;
                    continue;
                }
                cloneProbe?.ObserveFrame();
                deathProbe?.ObserveFrame(controller);
                embeddedProbe?.ObserveFrame(controller);
                absorptionProbe?.ObserveFrame(controller);
                stackProbe?.ObserveFrame(controller);
                randomHitProbe?.ObserveFrame(controller);
                masterChildProbe?.ObserveFrame(controller);
                hitFinishProbe?.ObserveFrame(controller);
                graphCapabilityProbe?.ObserveFrame(controller);
                explicitGraphProbe?.ObserveFrame(controller);
                if (coldSceneRequested && !coldSceneCompleted && graphCapabilityProbe.ReadyForDarkColdScene)
                {
                    runner.enabled = false;
                    yield return CaptureGridFrame("dark-status-restored");
                    captured = true;
                    if (SkillDarkStatusProcessValidation.PrepareRequested)
                    {
                        yield return SkillDarkStatusProcessValidation.Prepare(runner, saveRoot, slot, output,
                            graphCapabilityProbe, CaptureGridFrame);
                        yield break;
                    }
                    yield return SkillDarkStatusColdSceneValidation.Rebuild(home, runner, slot, saveRoot, output,
                        graphCapabilityProbe, value => settled = value, (nextHome, nextRunner) => {
                            home = nextHome; runner = nextRunner; controller = nextRunner.Controller;
                        }, CaptureGridFrame);
                    coldSceneCompleted = true;
                    continue;
                }
                if (explicitGraphProbe?.CaptureName != null)
                { yield return new WaitForEndOfFrame(); Capture(explicitGraphProbe.CaptureName); captured = true; }
                if (graphCapabilityProbe?.CaptureName != null)
                { yield return new WaitForEndOfFrame(); Capture(graphCapabilityProbe.CaptureName); captured = true; }
                if (hitFinishProbe?.CaptureName != null)
                { yield return new WaitForEndOfFrame(); Capture(hitFinishProbe.CaptureName); captured = true; }
                if (masterChildProbe?.CaptureName != null)
                { yield return new WaitForEndOfFrame(); Capture(masterChildProbe.CaptureName); captured = true; }
                if (randomHitProbe?.CaptureName != null)
                { yield return new WaitForEndOfFrame(); Capture(randomHitProbe.CaptureName); captured = true; }
                if (stackProbe?.CaptureName != null)
                { yield return new WaitForEndOfFrame(); Capture(stackProbe.CaptureName); captured = true; }
                if (absorptionProbe?.CaptureName != null)
                { yield return new WaitForEndOfFrame(); Capture(absorptionProbe.CaptureName); captured = true; }
                if (cloneProbe?.CaptureName != null)
                { yield return new WaitForEndOfFrame(); Capture(cloneProbe.CaptureName); captured = true; }
                if (deathProbe?.CaptureName != null)
                { yield return new WaitForEndOfFrame(); Capture(deathProbe.CaptureName); captured = true; }
                if (embeddedProbe?.CaptureName != null)
                { yield return new WaitForEndOfFrame(); Capture(embeddedProbe.CaptureName); captured = true; }
                if(!captured && probe != null && probe.ReadyToCapture)
                {yield return new WaitForEndOfFrame();Capture("summon-removal");captured=true;}
                yield return null;
            }
            if (settled != null) File.WriteAllText(Path.Combine(output,"summon-final.json"),FightBattleCheckpointSerializer.Save(settled));
            Require(captured && resultCaptured && settled!=null && settled.Finished,
                "Summon battle did not finish, leave its native result, or show its diagnostic graph.");
            Require(!coldSceneRequested || coldSceneCompleted, "Active dark status never completed its full host rebuild.");
            probe?.Verify("Windows Player",settled);
            cloneProbe?.Verify(settled);
            deathProbe?.Verify(settled);
            embeddedProbe?.Verify(settled);
            absorptionProbe?.Verify(settled);
            stackProbe?.Verify(settled);
            randomHitProbe?.Verify(settled);
            masterChildProbe?.Verify(settled);
            hitFinishProbe?.Verify(settled);
            graphCapabilityProbe?.Verify(settled);
            explicitGraphProbe?.Verify(settled);
            yield return new WaitForSecondsRealtime(.4f);
            Require(!FindObjectOfType<DemonResultWindow>(),"Native weekly result remained after simulated leave.");
            yield return new WaitForEndOfFrame();Capture("summon-return");
            File.WriteAllText(Path.Combine(output,"result.json"),JsonUtility.ToJson(new Result {passed=true},true));
            Destroy(home.gameObject);Application.Quit(0);
        }
        private static void Click(Button control)
        {
            Require(TryClickPoint(control, out var point, out var description),
                "Original start control has no exposed native hit area: " + description);
            var pointer=new PointerEventData(EventSystem.current) {button=PointerEventData.InputButton.Left,
                position=point};
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerUpHandler);
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);
        }
        private static bool TryClickPoint(Button control, out Vector2 point, out string description)
        {
            Canvas.ForceUpdateCanvases();
            var canvas=control.GetComponentInParent<Canvas>();
            var camera=canvas==null || canvas.renderMode==RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var rect=(RectTransform)control.transform;
            point=RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(rect.rect.center));
            var candidates=new List<Vector2> {point};
            foreach(var graphic in control.GetComponentsInChildren<Graphic>())
            {
                if(!graphic.raycastTarget) continue;
                var area=graphic.rectTransform;
                candidates.Add(RectTransformUtility.WorldToScreenPoint(camera,area.TransformPoint(area.rect.center)));
                // Native full-screen blank-space buttons sit behind the result model.
                // Search their actual hit area without changing graphic raycast settings.
                foreach(float y in new[] {.1f,.5f,.9f})
                    foreach(float x in new[] {.1f,.5f,.9f})
                        candidates.Add(RectTransformUtility.WorldToScreenPoint(camera,area.TransformPoint(
                            new Vector3(Mathf.Lerp(area.rect.xMin,area.rect.xMax,x),
                                Mathf.Lerp(area.rect.yMin,area.rect.yMax,y),0f))));
            }
            var hits=new List<RaycastResult>();
            foreach(var candidate in candidates)
            {
                hits.Clear();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) {position=candidate},hits);
                if(hits.Count>0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)==control.gameObject)
                {point=candidate;description=null;return true;}
            }
            description=control.name+" at "+point+"; screen="+Screen.width+"x"+Screen.height
                +"; canvas="+(canvas==null ? "null" : canvas.name+"/"+canvas.renderMode+"/enabled="+canvas.enabled)
                +"; groups="+string.Join(", ",control.GetComponentsInParent<CanvasGroup>(true)
                    .Select(value=>value.name+"/alpha="+value.alpha+"/raycast="+value.blocksRaycasts))
                +"; scene canvases="+string.Join(", ",Resources.FindObjectsOfTypeAll<Canvas>()
                    .Where(value=>value.gameObject.scene.IsValid()).Select(value=>value.name
                        +"/active="+value.gameObject.activeInHierarchy+"/enabled="+value.enabled
                        +"/scale="+value.scaleFactor+"/rect="+((RectTransform)value.transform).rect))
                +"; raycast="+string.Join(", ",hits.Select(value=>value.gameObject.name));
            return false;
        }
        private IEnumerator ClickGridControl(Selectable control)
        {
            float deadline=Time.realtimeSinceStartup+5f;
            while(!TryClickPoint((Button)control,out _,out _) && Time.realtimeSinceStartup<deadline) yield return null;
            yield return new WaitForEndOfFrame();Capture("grid-start-hit-area");
            Click((Button)control);yield return null;
        }
        private IEnumerator CaptureGridFrame(string name)
        { yield return new WaitForEndOfFrame(); Capture(name); }
        private void Capture(string name)
        {
            var texture=ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(output,name+".png"),texture.EncodeToPNG());Destroy(texture);
        }
        private static void Require(bool value,string message) {if(!value) throw new InvalidOperationException(message);}
        private void OnDestroy() {Application.logMessageReceived-=ObserveLog;}
    }
}
