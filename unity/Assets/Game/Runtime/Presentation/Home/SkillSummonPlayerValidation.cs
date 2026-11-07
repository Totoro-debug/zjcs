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
using Zjcs.Presentation.Character;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Home
{
    // Explicit Player diagnostic; all profile writes stay inside the supplied session directory.
    public sealed class SkillSummonPlayerValidation : MonoBehaviour
    {
        private string output;
        private Exception failure;
        private float deadline;
        [Serializable] private sealed class Result { public bool passed; public string error; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (Environment.GetEnvironmentVariable("ZJCS_SKILLS_VALIDATE_SUMMON_ONLY") != "1") return;
            var args=Environment.GetCommandLineArgs();
            int index=Array.IndexOf(args,"--zjcs-validate-skills");
            if(index<0 || index+1>=args.Length) return;
            var host=new GameObject("SkillSummonPlayerValidation");DontDestroyOnLoad(host);
            var driver=host.AddComponent<SkillSummonPlayerValidation>();
            driver.output=Path.GetFullPath(args[index+1]);Directory.CreateDirectory(driver.output);
            driver.deadline=Time.realtimeSinceStartup+300f;Application.runInBackground=true;
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
                    File.WriteAllText(Path.Combine(output,"result.json"),JsonUtility.ToJson(new Result {error=error.ToString()},true));
                    Debug.LogException(error);Application.Quit(1);yield break;
                }
                yield return next;
            }
        }
        private IEnumerator Run()
        {
            while(GameObject.Find("OfficialSaveSelectionPanel")==null) yield return null;
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
            save.Player.Experience=assets.Load().CumulativeLevelExperience[40];
            save.Player.Skills.Skills.Add(new Zjcs.Offline.Skills.OfflineOwnedSkill {ClassId=10014,Rank=1});
            File.WriteAllText(slots.GetProgressPath(slot),JsonUtility.ToJson(save,true));
            var session=OfflineMainlineProgressSession.OpenCharacter(slots,slot);
            var progression=new OfflineCharacterProgressionService(session,assets.Load());
            progression.InitializeWardrobe("skill-summon-player-wardrobe");
            progression.AttachSkillRules(assets.LoadSkillRules(),assets.LoadSkillRecommendations().recommendations);
            Require(progression.EquipSkill(10014,0).IsApplied,"Could not equip the summon Player skill.");
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
            var runner=home.StartWeeklyChallengeBattle(4800,cp=>settled=cp);
            var controller=runner.Controller;
            var probe=OfficialSummonLifecycleValidation.Configure(controller.Checkpoint);
            controller.StepCompleted+=step=>probe.ObserveStep(controller);
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
            yield return new WaitForEndOfFrame();Capture("summon-preparation");Click(start);
            bool captured=false;
            while(runner!=null)
            {
                if(!captured && probe.ReadyToCapture)
                {yield return new WaitForEndOfFrame();Capture("summon-removal");captured=true;}
                yield return null;
            }
            Require(captured && settled!=null && settled.Finished,"Summon battle did not finish.");
            probe.Verify("Windows Player",settled);
            File.WriteAllText(Path.Combine(output,"summon-final.json"),FightBattleCheckpointSerializer.Save(settled));
            yield return new WaitForSecondsRealtime(.4f);
            yield return new WaitForEndOfFrame();Capture("summon-result");
            File.WriteAllText(Path.Combine(output,"result.json"),JsonUtility.ToJson(new Result {passed=true},true));
            Destroy(home.gameObject);Application.Quit(0);
        }
        private static void Click(Button control)
        {
            Canvas.ForceUpdateCanvases();var rect=(RectTransform)control.transform;
            var pointer=new PointerEventData(EventSystem.current) {button=PointerEventData.InputButton.Left,
                position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center))};
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
            Require(hits.Count>0 && (hits[0].gameObject==control.gameObject || hits[0].gameObject.transform.IsChildOf(control.transform)),
                "Original start control is obstructed.");
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerUpHandler);
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);
        }
        private static IEnumerator ClickGridControl(Selectable control)
        { Click((Button)control); yield return null; }
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
