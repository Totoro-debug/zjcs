public sealed partial class HomeCurrencyWindowsNativeRunner
{
    void CheckTaskConditionNotifications(Zjcs.Offline.Progression.OfflineMainlineProgressSession profile,
        Zjcs.Offline.HomeTaskUI.OfflineHomeTaskConditionReader reader,
        Zjcs.Offline.HomeHudUI.OfflineHomeMainlineTaskData data,
        Zjcs.Official.HomeTaskUI.ICondition.DataHub hub)
    {
        var player=new Zjcs.Offline.HomeUI.OfflineHomeConditionPlayer(
            Zjcs.Offline.HomeUI.OfficialCurrentHomeRuleAssetCatalog.LoadDefault(),()=>profile.Save,_=>false);
        var failures=new System.Collections.Generic.List<System.Exception>();
        var conditions=new Zjcs.Offline.HomeTaskUI.OfflineHomeTaskConditions(reader,player,failures.Add);
        var calls=new System.Collections.Generic.List<string>();
        var leaves=new System.Collections.Generic.Dictionary<string,Zjcs.Official.HomeTaskUI.ICondition>();
        foreach(var row in data.OrderedTasks){
            string type=(string)row.CopyOriginalRow()["FinishCondition"]["_type"];
            if(leaves.ContainsKey(type))continue;
            var condition=reader.Finish(row.ClassId);leaves.Add(type,condition);
            string label=type;conditions.Register(condition,()=>calls.Add(label));
        }
        var submitted=reader.Display(1005);
        conditions.Register(submitted,()=>calls.Add("TaskSubmitCondition"));
        System.Action<string,System.Action,string[]> check=(label,notify,expected)=>{
            calls.Clear();notify();Require(System.Linq.Enumerable.SequenceEqual(calls,expected),label+": original ordered task condition notifications");
        };
        check("blackboard",conditions.NotifyUpdateBlackboardData,new[]{"MapGameTotalCountCondition","MapLogicEntityEndByTagCondition"});
        check("world",conditions.NotifyWorldMapProgressUpdate,new[]{"WorldMapProgressCondition"});
        check("other active entity",()=>conditions.NotifyActiveableEntUpdate(false),new[]{"WorldMapProgressCondition"});
        check("goddess",()=>conditions.NotifyActiveableEntUpdate(true),new[]{"MapRegionGoddessCondition","WorldMapProgressCondition"});
        check("profession",conditions.NotifyProfessionPromote,new[]{"MaxProfessionRankCondition"});
        check("chest",conditions.NotifyOpenChest,new[]{"TotalOpenChestCondition"});
        check("tower",conditions.NotifyDemonBattlefieldLayerUpdate,new[]{"DemonBattleLayerPassCondition"});
        check("task removed",conditions.NotifyTaskRemove,new[]{"TaskSubmitCondition"});
        check("task updated",conditions.NotifyTaskUpdate,System.Array.Empty<string>());
        check("task added",conditions.NotifyTaskAdd,System.Array.Empty<string>());
        check("finished ID added",conditions.NotifyAddFinishedTask,System.Array.Empty<string>());

        var battle=leaves["MapGameTotalCountCondition"];int count=0;long observed=-1;
        System.Action refresh=()=>{count++;observed=battle.GetProgress(hub).Item1;};
        conditions.Register(battle,refresh);conditions.Register(battle,refresh);
        Require(profile.RecordMapBattle(10,0,1,"condition-notify-battle").IsApplied,"commit real battle before its event");
        conditions.NotifyUpdateBlackboardData();Require(count==1&&observed==4,"reference-counted callback reads the replaced committed Save once");
        conditions.Deregister(battle,refresh);conditions.NotifyUpdateBlackboardData();Require(count==2,"one original reference remains");
        conditions.Deregister(battle,refresh);conditions.NotifyUpdateBlackboardData();Require(count==2,"last reference removes callback");
        System.Action broken=()=>throw new System.InvalidOperationException("expected task subscriber failure");
        conditions.Register(battle,broken);calls.Clear();conditions.NotifyUpdateBlackboardData();
        Require(failures.Count==1&&calls.Contains("MapLogicEntityEndByTagCondition"),"original exception isolation still refreshes the next condition group");
        conditions.Deregister(battle,broken);
        var foreignReader=new Zjcs.Offline.HomeTaskUI.OfflineHomeTaskConditionReader(profile,data,_=>0,()=>6000);
        bool rejected=false;try{conditions.Register(foreignReader.Finish(1000),refresh);}catch(System.ArgumentException){rejected=true;}
        Require(rejected,"reject a task condition from a different player scope");
        conditions.Dispose();conditions.Dispose();conditions.Deregister(battle,refresh);
        rejected=false;try{conditions.NotifyProfessionPromote();}catch(System.ObjectDisposedException){rejected=true;}
        Require(rejected,"destroy the registry and reject later player events");
        UnityEngine.Debug.Log("HOME_TASK_CONDITION_NOTIFICATIONS_PASSED: original event grouping/order, Goddess branch, actual committed profile reads, reference counts, isolated subscriber failure, player-scope guard and disposal. Player-owner event forwarding and native row composition remain separate.");
    }
}
