public sealed partial class HomeCurrencyWindowsNativeRunner
{
    void CheckTaskConditionReader()
    {
        string root=System.IO.Path.Combine(System.Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),"task-condition-profile-"+System.Guid.NewGuid().ToString("N"));
        try {
            var creation=Zjcs.Offline.CharacterCreation.CharacterCreationOfflineService.OpenDefault(root);
            string slot=creation.CreateSlot();var draft=creation.OpenDraft(slot);draft.name="任务条件验证";
            Require(creation.TryCreateCharacter(slot,draft,out string error),error);
            var slots=new Zjcs.Offline.State.ProfileSlotCatalog(root);
            var catalog=Zjcs.Offline.Progression.OfficialMainlineTaskCatalog.LoadDefault();
            var profile=Zjcs.Offline.Progression.OfflineMainlineProgressSession.Open(slots,slot,catalog);
            var data=Zjcs.Offline.HomeHudUI.OfflineHomeMainlineTaskData.LoadDefault();
            int layer=0; // Explicit tower test input; live tower integration remains separate.
            int gacha=(int)Newtonsoft.Json.Linq.JObject.Parse(assets.configuration.text)["tables"]["game_settings"]["GachaId"];
            var reader=new Zjcs.Offline.HomeTaskUI.OfflineHomeTaskConditionReader(profile,data,_=>layer,()=>gacha);
            var hub=new Zjcs.Official.HomeTaskUI.ICondition.DataHub();
            string path=slots.GetProgressPath(slot),before=System.IO.File.ReadAllText(path);
            Require(reader.Display(1000)==null&&!reader.Display(1005).IsMet(hub),"original absent first display condition and unsubmitted successor");
            foreach(var row in data.OrderedTasks)Require(reader.Finish(row.ClassId)!=null,"all 55 original finish conditions project");
            var battle=reader.Finish(1000);var progress=battle.GetProgress(hub);
            Require(progress==(0L,2L)&&!battle.IsMet(hub)&&profile.Save.World.Maps.Count==0,"new-character original blackboard zero");
            var desc=battle.GetDescComponent(hub,null,"#00FF00","#FF0000");
            Require(string.Format(data.Text(desc.Key),desc.Args)=="初始岛一战斗胜利：0/2"&&desc.TextProcessor.Process("<color={c}>x</color>")=="<color=#FF0000>x</color>","original dynamic battle text and unmet condition color processor");
            var world=reader.Finish(1005);
            Require(world is Zjcs.Official.HomeTaskUI.WorldMapProgressCondition&&world.GetProgress(hub)==(0L,100L)&&!world.IsMet(hub),"original world condition subtype and unvisited percentage");
            desc=world.GetDescComponent(hub,null,null,null);
            Require(desc.Args[0].ToString()=="初始岛一"&&(string)desc.Args[1]=="0%"&&(string)desc.Args[2]=="100%"&&string.Format(data.Text(desc.Key),desc.Args)=="探索初始岛一","original region culture argument and percentage description");
            Require(desc.TextProcessor.Process("{c}")=="{c}","original unchanged processor for missing colors");
            Require(System.IO.File.ReadAllText(path)==before&&profile.Save.World.Maps.Count==0,"condition reads never write or create missing map records");
            Require(profile.InitializeMap(10,new[]{0,1,2,3,4}).IsApplied,"initialize actual map data");
            Require(profile.SetMapRegionProgress(10,0,9999).IsApplied&&world.GetProgress(hub)==(99L,100L)&&!world.IsMet(hub),"original float percentage truncation immediately below completion");
            Require(profile.SetMapRegionProgress(10,0,10000).IsApplied&&world.GetProgress(hub)==(100L,100L)&&world.IsMet(hub),"original complete percentage from actual saved exploration");
            for(int index=0;index<3;index++)Require(profile.RecordMapBattle(10,0,1,"condition-battle-"+index).IsApplied,"actual battle-count receipt");
            Require(battle.GetProgress(hub)==(3L,2L)&&battle.IsMet(hub),"raw battle count remains above the goal");
            desc=battle.GetDescComponent(hub,null,"#00FF00","#FF0000");
            Require((long)desc.Args[0]==2&&(long)desc.Args[1]==2&&desc.TextProcessor.Process("{c}")=="#00FF00","original description alone clamps and selects met color");
            Require(profile.ClaimTask(1000).IsApplied&&reader.Display(1005).IsMet(hub),"display condition observes the actual committed task receipt");
            var chest=reader.Finish(1040);
            for(int index=0;index<12;index++)Require(profile.RecordChestOpened(6000,"condition-chest-"+index).IsApplied,"actual chest event receipt");
            Require(chest.GetProgress(hub)==(12L,10L)&&chest.IsMet(hub),"original raw chest count above target");
            desc=chest.GetDescComponent(hub,null,null,null);Require((long)desc.Args[0]==10,"original chest description clamps independently");
            Require(profile.ApplyCharacterMutation(player=>player.MaxProfessionRank=0,"condition-rank-zero").IsApplied,"persist actual locked profession progress");
            var rank=reader.Finish(1030);Require(rank.GetProgress(hub)==(0L,1L)&&!rank.IsMet(hub),"confirmed actual rank starts below threshold");
            Require(profile.ApplyCharacterMutation(player=>player.MaxProfessionRank=2,"condition-rank-unlock").IsApplied,"persist actual highest profession rank");
            Require(rank.GetProgress(hub)==(1L,1L)&&rank.IsMet(hub),"original rank progress is boolean rather than rank/goal");
            var tower=reader.Finish(1070);layer=12;
            Require(tower.GetProgress(hub).Item1==13,"original client tower progress adds one without truncating raw layer");
            desc=tower.GetDescComponent(hub,null,null,null);Require((long)desc.Args[0]==(long)desc.Args[1],"tower description clamps after the client layer offset");
            var restored=Zjcs.Offline.Progression.OfflineMainlineProgressSession.Open(slots,slot,catalog);
            var reloaded=new Zjcs.Offline.HomeTaskUI.OfflineHomeTaskConditionReader(restored,data,_=>layer,()=>gacha);
            Require(reloaded.Display(1005).IsMet(hub)&&reloaded.Finish(1005).GetProgress(hub)==(100L,100L)&&reloaded.Finish(1000).GetProgress(hub)==(3L,2L)&&reloaded.Finish(1040).GetProgress(hub)==(12L,10L),"real profile reopening preserves all observed source progress");
            Require(reloaded.Finish(1030).GetProgress(hub)==(1L,1L),"confirmed highest profession rank restores from the real profile");
            UnityEngine.Debug.Log("HOME_TASK_CONDITION_READER_PASSED: actual new-character save, all 55 condition projections, original raw/description progress, float percentages, dynamic CN text/colors, actual event/claim receipts, confirmed live profession rank and reopening. Tower callback is controlled; native row/notification integration remains separate.");
        }
        finally { if(System.IO.Directory.Exists(root))System.IO.Directory.Delete(root,true); }
    }
}
