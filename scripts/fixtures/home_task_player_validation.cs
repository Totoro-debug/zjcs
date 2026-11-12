public sealed partial class HomeCurrencyWindowsNativeRunner
{
    System.Collections.IEnumerator ExerciseTaskPlayer()
    {
        string root=System.IO.Path.Combine(System.Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),"task-player-profile-"+System.Guid.NewGuid().ToString("N"));
        Zjcs.Offline.HomeTaskUI.OfflineHomeTaskPlayer player=null;
        Zjcs.Offline.HomeTaskUI.OfflineHomeTaskConditions conditions=null;
        Zjcs.Offline.HomeTaskUI.OfflineHomeTaskRewardIcons pool=null;
        UnityEngine.GameObject rewardSlot=null;
        int beforeSubscriptions=subscribers;
        try {
            var creation=Zjcs.Offline.CharacterCreation.CharacterCreationOfflineService.OpenDefault(root);
            string slot=creation.CreateSlot();var draft=creation.OpenDraft(slot);draft.name="任务验证";
            Require(creation.TryCreateCharacter(slot,draft,out string error),error);
            var slots=new Zjcs.Offline.State.ProfileSlotCatalog(root);
            var catalog=Zjcs.Offline.Progression.OfficialMainlineTaskCatalog.LoadDefault();
            var profile=Zjcs.Offline.Progression.OfflineMainlineProgressSession.Open(slots,slot,catalog);
            var data=Zjcs.Offline.HomeHudUI.OfflineHomeMainlineTaskData.LoadDefault();taskIconData=data;
            var settings=(Newtonsoft.Json.Linq.JObject)Newtonsoft.Json.Linq.JObject.Parse(assets.configuration.text)["tables"]["game_settings"];
            var reader=new Zjcs.Offline.HomeTaskUI.OfflineHomeTaskConditionReader(profile,data,_=>0,()=>(int)settings["GachaId"]);
            var failures=new System.Collections.Generic.List<System.Exception>();
            conditions=new Zjcs.Offline.HomeTaskUI.OfflineHomeTaskConditions(reader,
                new Zjcs.Offline.HomeUI.OfflineHomeConditionPlayer(Zjcs.Offline.HomeUI.OfficialCurrentHomeRuleAssetCatalog.LoadDefault(),()=>profile.Save,_=>false),failures.Add);
            var rewards=new Zjcs.Offline.HomeTaskUI.OfflineHomeTaskRewardPresenter(data,settings);
            var results=new System.Collections.Generic.List<Zjcs.Offline.Progression.ProgressionResult>();
            player=new Zjcs.Offline.HomeTaskUI.OfflineHomeTaskPlayer(profile,data,reader,conditions,rewards,
                new TaskPlayerNavigationProbe(),()=>0,()=>default(Zjcs.Official.HomeTaskUI.SubRank),results.Add,failures.Add);
            var first=player.GetTaskData(1000);
            Require(first!=null&&first.TaskId==1000&&first.TaskClassId==1000&&object.ReferenceEquals(first.DataWrap,first)
                &&first.DataWrap.Pinned&&player.IsPinnedTask(1000)&&!first.IsCompletedState()&&!first.CanFinish(player)
                &&!first.CanGetReward(player)&&!first.ShouldShowCountdown()&&first.GetRemainTime()==null,
                "real new-character task model retains selected identity, pin flags, state and duration branches");
            Require(System.Linq.Enumerable.Count(player.PinTasks())==1&&player.GetTaskData(1005)==null,
                "only acquired mainline instances enter the original pinned-task projection");
            var hub=new Zjcs.Official.HomeTaskUI.ICondition.DataHub();
            Require(first.Info().DisplayCondition==null&&first.Info().Order==5&&!first.Info().ShowProgressBar
                &&first.Info().IsAllowTrace&&!first.Info().AutoTracing&&first.Info().FinishCondition.GetProgress(hub)==(0L,2L),
                "native task interface reads original flags and actual fresh progress");

            pool=new Zjcs.Offline.HomeTaskUI.OfflineHomeTaskRewardIcons(canvas.transform,this,fonts,Post,data,assets);
            rewardSlot=new UnityEngine.GameObject("ActualTaskRewardPreview",typeof(UnityEngine.RectTransform));
            rewardSlot.transform.SetParent(canvas.transform,false);
            var icons=new System.Collections.Generic.List<Zjcs.Official.HomeTaskUI.ItemIcon>();
            first.InitRewardIconWithProbability(pool,rewardSlot.transform,icons);
            Require(icons.Count==1&&icons[0].myInfo.ClassId==2,
                "original model preview creates the real task-1000 fixed native reward");
            var native=((Zjcs.Offline.HomeTaskUI.OfflineHomeTaskRewardIcon)icons[0]).Native;
            Require(System.Linq.Enumerable.Any(native.GetComponentsInChildren<TMPro.TMP_Text>(true),text=>text.text=="30"),
                "actual original count text shows the configured thirty-item reward");
            native.SetAttachment(Zjcs.Official.ProfessionUI.ItemIconAttachmentType.ProbabilityTip,true);
            yield return null;
            var probability=native.GetComponentInChildren<Zjcs.Official.ProfessionUI.ItemIconAttachment>(true)
                .GetAttachment(Zjcs.Official.ProfessionUI.ItemIconAttachmentType.ProbabilityTip);
            var languageControl=probability.GetComponent<Zjcs.Official.HomeTaskUI.MultiLanguageTransformControler>();
            Require(probability.gameObject.activeInHierarchy&&languageControl
                &&UnityEngine.Mathf.Approximately(probability.transform.localScale.x,1)
                &&System.Linq.Enumerable.All(probability.GetComponentsInChildren<UnityEngine.MonoBehaviour>(true),component=>component),
                "original probability attachment resolves all native components and Chinese transform settings");
            Require(System.Linq.Enumerable.Any(probability.GetComponentsInChildren<TMPro.TMP_Text>(true),text=>text.text=="概率"),
                "original probability attachment localizes its actual CN label");
            languageControl.LanguageTypeService.CurrentLanguage=Zjcs.Official.World.NativeLabelUI.Framework.LanguageType.en_US;
            Require(UnityEngine.Mathf.Approximately(probability.transform.localScale.x,1.3f),"original language-change observer applies the West transform group");
            pool.RefreshLanguage();Require(UnityEngine.Mathf.Approximately(probability.transform.localScale.x,1),"real language owner restores the Chinese transform group");
            native.SetAttachment(Zjcs.Official.ProfessionUI.ItemIconAttachmentType.ProbabilityTip,false);
            native.SetAttachment(Zjcs.Official.ProfessionUI.ItemIconAttachmentType.GotTip,true);
            Require(native.GetComponentInChildren<Zjcs.Official.ProfessionUI.ItemIconAttachment>(true).GetAttachment(
                Zjcs.Official.ProfessionUI.ItemIconAttachmentType.GotTip).gameObject.activeInHierarchy,"original obtained attachment loads and activates");
            native.SetAttachment(Zjcs.Official.ProfessionUI.ItemIconAttachmentType.GotTip,false);
            foreach(var icon in icons){icon.ResetCountTextScale();pool.RecycleIcon(icon);}icons.Clear();
            rewards.Populate(1020,pool,rewardSlot.transform,icons);
            Require(System.Linq.Enumerable.SequenceEqual(System.Linq.Enumerable.Select(icons,icon=>icon.myInfo.ClassId),new[]{50,2,41300}),
                "original designated experience, money and other-item reward ordering");
            for(int index=0;index<icons.Count;index++)icons[index].GetComponent<UnityEngine.RectTransform>().anchoredPosition=new UnityEngine.Vector2(-200+index*200,0);
            yield return null;Capture("home-task-model-rewards-native.png");
            int beforeDetails=details;foreach(var icon in icons)Click(((Zjcs.Offline.HomeTaskUI.OfflineHomeTaskRewardIcon)icon).Native.Button);
            Require(details==beforeDetails+3,"original model reward pointers reach the explicit detail host");
            foreach(var icon in icons){icon.ResetCountTextScale();pool.RecycleIcon(icon);}icons.Clear();

            int updates=0,adds=0,removes=0,displayEvents=0,itemEvents=0;long observedPrevious=-1;
            System.Action<Zjcs.Official.HomeTaskUI.PlayerTaskDataWrap> update=task=>{updates++;Require(task.TaskId==1000&&task.IsCompletedState(),"update observes coherent committed Completed state");};
            player.OnTaskUpdate+=update;player.OnTaskUpdate+=update;
            System.Action<Zjcs.Official.HomeTaskUI.PlayerTaskDataWrap> failing=task=>throw new System.InvalidOperationException("expected isolated task listener");
            player.OnTaskUpdate+=failing;
            player.OnTaskAdd+=task=>{adds++;Require(task.TaskId==1005,"actual next acquisition");};
            player.OnTaskRemove+=id=>{removes++;Require(id==1000&&player.GetTaskData(id)==null,"received task is absent before the remove callback");};
            player.OnItemCountUpdate+=(item,previous,source)=>{if(item.Info.ClassId==2){itemEvents++;observedPrevious=previous;}};
            Require(profile.InitializeMap(10,new[]{0,1,2,3,4}).IsApplied
                &&profile.RecordMapBattle(10,0,1,"task-player-battle-1").IsApplied
                &&profile.RecordMapBattle(10,0,1,"task-player-battle-2").IsApplied,"commit actual task progress");
            player.RefreshTasks(false);conditions.NotifyUpdateBlackboardData();
            Require(updates==1&&adds==1&&failures.Count==1&&first.CanGetReward(player)
                &&System.Linq.Enumerable.Count(player.PinTasks())==2,
                "original safe events deduplicate, isolate errors and add the acquired successor");
            player.OnTaskUpdate-=update;player.OnTaskUpdate-=failing;
            var next=player.GetTaskData(1005);Require(next!=null&&!next.Info().DisplayCondition.IsMet(hub)
                &&next.Info().ShowProgressBar&&next.Info().FinishCondition.GetProgress(hub)==(0L,100L),
                "original display waits for submission while the acquired native world task uses percentages");
            int newlyVisible=0;player.OnTaskUpdate+=task=>{if(task.TaskClassId==1005){newlyVisible++;Require(task.Info().DisplayCondition.IsMet(hub),"updated task instance reads the new display state");}};
            conditions.Register(next.Info().DisplayCondition,()=>{displayEvents++;Require(next.Info().DisplayCondition.IsMet(hub),"submit observer reads committed source state");});
            long beforeMoney=profile.InventoryCount(2);
            var external=Zjcs.Offline.Progression.OfflineMainlineProgressSession.Open(slots,slot,catalog);
            Require(external.ApplyCharacterMutation(value=>{
                var stack=System.Linq.Enumerable.FirstOrDefault(value.Inventory,item=>item.ItemId==2);
                if(stack==null)value.Inventory.Add(new Zjcs.Offline.Progression.OfflineItemStack{ItemId=2,Count=7});else stack.Count+=7;
            },"task-player-external-award").IsApplied,"another actual profile service awards currency before task submission");
            first.Submit(player,1);
            Require(results.Count==1&&results[0].IsApplied&&removes==1&&displayEvents==1&&itemEvents==1
                &&failures.Count==1&&newlyVisible==1&&observedPrevious==beforeMoney+7&&player.GetItemAmount(2)==beforeMoney+37
                &&System.Linq.Enumerable.Count(player.PinTasks())==1&&player.GetTaskData(1005)!=null,
                "real task submission reloads shared inventory, awards once and publishes remove/display/item events");
            first.Submit(player,1);
            Require(results.Count==2&&results[1].Status==Zjcs.Offline.Progression.ProgressionResultStatus.Unchanged
                &&removes==1&&itemEvents==1&&displayEvents==1&&newlyVisible==1,"replayed submission emits no duplicate committed changes");
            var restored=Zjcs.Offline.Progression.OfflineMainlineProgressSession.Open(slots,slot,catalog);
            Require(restored.InventoryCount(2)==beforeMoney+37&&restored.Save.Tasks.ClaimedMainlineTaskIds.Contains(1000),
                "actual task reward and submitted ID survive reopening");
            player.OnTaskUpdate-=update;player.OnTaskUpdate-=update;
            player.Dispose();player.Dispose();bool rejected=false;try{player.RefreshTasks();}catch(System.ObjectDisposedException){rejected=true;}
            Require(rejected,"disposed task player rejects subsequent refresh");
            UnityEngine.Debug.Log("HOME_TASK_PLAYER_PASSED: actual new-character acquisition/model flags, original pooled fixed-reward preview/order/counts and detail pointer, selected SafeAction deduplication/error isolation, actual completion and submission events, shared-profile reload, receipt replay and restoration. Navigation, combat/rank ports and detail presentation are controlled; native row and production HUD composition remain separate.");
        }
        finally {
            player?.Dispose();conditions?.Dispose();pool?.Dispose();taskIconData=null;
            if(rewardSlot)UnityEngine.Object.Destroy(rewardSlot);
            if(System.IO.Directory.Exists(root))System.IO.Directory.Delete(root,true);
        }
        yield return null;Require(subscribers==beforeSubscriptions,"actual task-player reward scope leaves no item observers");
    }

    sealed class TaskPlayerNavigationProbe : Zjcs.Offline.HomeTaskUI.IOfflineHomeTaskNavigation
    {
        public bool CanNavigate(int id)=>false;
        public void Navigate(int id,Zjcs.Official.HomeTaskUI.IMapInputSupportHandlerProxy input,Zjcs.Official.HomeTaskUI.INavigateManager navigation,object context)
            =>throw new System.NotSupportedException("Controlled navigation boundary; actual map integration is separate.");
        public Zjcs.Official.HomeTaskUI.ITaskTraceTarget TraceTarget(int id)=>null;
    }
}
