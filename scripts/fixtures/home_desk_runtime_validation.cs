using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.CharacterCreation;
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Official.HomeFurniture;
using Zjcs.Official.HomeUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.Home;
using Object = UnityEngine.Object;

public static partial class HomeDeskRuntimeValidation
{
    private static DeskFurniture Desk => home.NativeFurnitureInstance(EFurniture.Desk)?.GetComponentInChildren<DeskFurniture>(true);
    private static void FocusDesk()
    {
        Require(Desk != null, "original DeskFurniture exists in the native slot");
        Vector3 position = home.Scene.Position;
        position.x = home.NativeFurnitureInstance(EFurniture.Desk).transform.position.x;
        home.Scene.Camera.transform.position = home.Scene.Clamp(position);
        Physics.SyncTransforms();
    }
    private static IEnumerator RunDeskChecks()
    {
        checksCount = 0;
        string saveRoot = Path.Combine(Work, "isolated-saves");
        var creation = CharacterCreationOfflineService.OpenDefault(saveRoot);
        string slot = creation.CreateSlot();
        var draft = creation.OpenDraft(slot);
        draft.name = "工作台验证";
        Require(creation.TryCreateCharacter(slot, draft, out string error), "create isolated character: " + error);
        Require(creation.TrySetInitialWeapon(slot, CharacterCreationOfflineService.SwordInitialWeaponType, out error), "set initial weapon: " + error);
        var profile = OfflineMainlineProgressSession.OpenCharacter(new ProfileSlotCatalog(saveRoot), slot);
        var character = OfficialCharacterAssetCatalog.LoadDefault().Load();
        var pointer = new NativePointer();
        Action<int> level = value => {
            profile.Reload();
            Require(profile.ApplyCharacterMutation(p => {
                p.Level = value; p.Experience = character.CumulativeLevelExperience[value];
                p.SubRank = value >= 30 ? 4 : value >= 20 ? 3 : value >= 10 ? 2 : 1; p.Rank = 1;
                if (!p.UnlockedMapClassIds.Contains(11)) p.UnlockedMapClassIds.Add(11);
            }).IsApplied, "seed isolated level " + value);
        };
        level(1);
        home = OpenHome(slot, saveRoot, pointer);
        yield return WaitFrames(8);
        FocusDesk();
        Require(Desk.GetState() == FurnitureBase.EFurnitureState.Locked, "without treasure or level qualification the workbench stays locked");
        var definition = home.Rules.FurnitureOf((int)EFurniture.Desk);
        Require(string.IsNullOrEmpty(definition.repairStory) && (definition.repairCost == null || definition.repairCost.Length == 0), "original workbench has no exclusive repair story or material cost");
        yield return Capture("01-desk-locked.png");
        home.Close(); home = null; yield return WaitFrames(3);

        // The official OR branch opens the same repair state when an intact
        // treasure is owned, independently of the level-thirty branch.
        int treasureId = OfflineTreasureCatalog.Load().Treasures.First().ClassId;
        profile.Reload();
        Require(profile.ApplyInventoryTransaction("desk:owned-treasure", save =>
            save.Player.Inventory.Add(new OfflineItemStack { ItemId = treasureId, Count = 1 })).IsApplied, "seed an actual configured treasure");
        home = OpenHome(slot, saveRoot, pointer); yield return WaitFrames(8); FocusDesk();
        Require(Desk.GetState() == FurnitureBase.EFurnitureState.CanRepair, "owned treasure unlocks workbench repair at level one");
        yield return Capture("02-desk-owned-treasure-unlock.png");
        home.Close(); home = null; yield return WaitFrames(3);
        profile.Reload();
        Require(profile.ApplyInventoryTransaction("desk:remove-fixture-treasure", save =>
            save.Player.Inventory.RemoveAll(row => row.ItemId == treasureId)).IsApplied, "clear the treasure branch fixture");

        level(18);
        home = OpenHome(slot, saveRoot, pointer); yield return WaitFrames(8); FocusDesk();
        var deskInfo = Desk.Services.GetFurnitureInfo(EFurniture.Desk);
        Debug.Log("DESK_NOTICE_STATE: " + Desk.GetState()
            + "; notice=" + deskInfo.RepairNoticeCondition.IsMet()
            + "; unlock=" + deskInfo.UnlockCondition.IsMet()
            + "; profile=" + home.Profile.Save.Player.Level + "/" + home.Profile.Save.Player.SubRank
            + "; maps=" + string.Join(",", home.Profile.Save.Player.UnlockedMapClassIds));
        Require(Desk.GetState() == FurnitureBase.EFurnitureState.RepairNotice, "level eighteen with map eleven unlocked shows the official repair notice");
        Require(Desk.GetComponentInChildren<HomeNotice>(true).ShowNotice, "native workbench notice is visible");
        yield return Capture("03-desk-repair-notice.png");
        home.Close(); home = null; yield return WaitFrames(3);

        level(30);
        home = OpenHome(slot, saveRoot, pointer); yield return WaitFrames(8); FocusDesk();
        Require(Desk.GetState() == FurnitureBase.EFurnitureState.CanRepair, "level thirty reaches the original repair state");
        Require(!Desk.GetComponentInChildren<HomeNotice>(true).ShowNotice, "notice hides after actual unlock");
        var manager = Field<ITipDotNodeManager>(Field<OfflinePartnerTipDotRuntime>(home, "partnerTipDots"), "manager");
        Require(manager is IDynamicTipDotNodeManager, "the actual native graph manager implements the shared dynamic contract");
        CheckDynamicNodeManager((IDynamicTipDotNodeManager)manager);
        int settingEvents = 0;
        using (home.HomeTipServices.SubscribeTreasureSettingUpdate(() => settingEvents++))
        {
            bool previous = home.HomeTipServices.AutoMergeWhenLevelUp;
            home.HomeTipServices.AutoMergeWhenLevelUp = !previous;
            home.HomeTipServices.AutoMergeWhenLevelUp = !previous;
            home.HomeTipServices.AutoMergeWhenLevelUp = previous;
            Require(settingEvents == 2, "treasure setting changes notify once and same-value assignments stay silent");
        }
        home.HomeTipServices.AwakeAutoMergeEnable = !home.HomeTipServices.AwakeAutoMergeEnable;
        Require(settingEvents == 2, "disposed treasure setting subscriptions receive no callback");
        yield return Capture("04-desk-can-repair.png");
        pointer.ClickWorldPoint(home, FindTouchCollider(Desk).bounds.center);
        FurnitureRepairDialog repair = null;
        float deadline = Time.realtimeSinceStartup + 10;
        while (Time.realtimeSinceStartup < deadline)
        {
            repair = Prop<FurnitureRepairDialog>(home.Hud, "ActiveRepairDialog");
            if (repair && repair.GetComponent<WorldPopupWindow>().State == WorldPopupWindow.EState.Show) break;
            yield return null;
        }
        Require(repair && !home.IsFurnitureStoryRunning, "native workbench click opens the real repair window directly");
        yield return Capture("05-desk-native-repair.png");
        ClickUiButton(repair.RepairBtn);
        deadline = Time.realtimeSinceStartup + 10;
        while ((!home.Session.OwnsFurniture((int)EFurniture.Desk) || Desk.GetState() != FurnitureBase.EFurnitureState.Functional)
            && Time.realtimeSinceStartup < deadline) yield return null;
        Require(home.Session.OwnsFurniture((int)EFurniture.Desk) && Desk.GetState() == FurnitureBase.EFurnitureState.Functional,
            "native repair button commits workbench ownership and runs the functional state transition");
        yield return WaitFrames(8);
        yield return Capture("06-desk-repaired.png");
        home.Hud.CloseDialog(); yield return WaitFrames(4);
        home.Close(); home = null; yield return WaitFrames(3);
        home = OpenHome(slot, saveRoot, pointer); yield return WaitFrames(8); FocusDesk();
        Require(home.Session.OwnsFurniture((int)EFurniture.Desk) && Desk.GetState() == FurnitureBase.EFurnitureState.Functional,
            "actual cold reopen preserves workbench ownership and native functional state");
        yield return Capture("07-desk-cold-reopen.png");

        var skins = home.Rules.Data.furnitureSkins.Where(row => row.furnitureType == (int)EFurniture.Desk).ToArray();
        Require(skins.Length == 6, "all six official workbench skins are present");
        foreach (var skin in skins)
        {
            Require(home.HomeProgression.EquipSkin(home.Session, 0, (int)EFurniture.Desk, skin.skinItemId, _ => 1, "desk:skin:" + skin.skinItemId), "equip actual workbench skin " + skin.skinItemId);
            home.Scene.ApplyHomeSkins(home.Catalog.HomeSkins, home.Session.Skins, home.Rules);
            home.RefreshNativeFurnitureSkins(); yield return WaitFrames(6); FocusDesk();
            var live = home.NativeFurnitureInstance(EFurniture.Desk);
            Require(live && live.transform.parent.name == "DeskSlot" && Desk.GetState() == FurnitureBase.EFurnitureState.Functional,
                "official workbench skin mounts under the original DeskSlot: " + skin.skinItemId);
            Require(live.GetComponentsInChildren<Component>(true).All(value => value != null), "skin has no missing scripts: " + skin.skinItemId);
            Require(live.GetComponentsInChildren<Renderer>(true).Any(value => value.sharedMaterials.Any(material => material != null)), "skin binds actual original materials: " + skin.skinItemId);
            yield return Capture("08-desk-skin-" + skin.skinItemId + ".png");
        }
        home.Close(); home = null; yield return WaitFrames(3);
    }

    private sealed class DynamicProbe : Zjcs.Official.HomeTipDots.TipDotDynamicLinkNodeBase
    {
        internal int Added, Removed;
        public override void InitLinkNode() { }
        protected override void OnAddNode(TipDotNodeKey key) { Added++; }
        protected override void OnReduceNode(TipDotNodeKey key) { Removed++; }
    }
    private static void CheckDynamicNodeManager(IDynamicTipDotNodeManager manager)
    {
        Action<TipDotNodeKey> addedBefore = manager.OnAddNode, removedBefore = manager.OnReduceNode;
        var probe = new DynamicProbe { NodeManager = manager };
        probe.Init();
        var key = new TipDotNodeKey("__DeskFixture", "probe", BaseNodeInstanceInfo.kDefault);
        manager.OnAddNode?.Invoke(key);
        manager.OnReduceNode?.Invoke(key);
        Require(probe.Added == 1 && probe.Removed == 1, "Home dynamic node subscribes to the real partner graph manager");
        probe.Release();
        Require(manager.OnAddNode == addedBefore && manager.OnReduceNode == removedBefore,
            "dynamic node release restores both original callback chains");
        manager.OnAddNode?.Invoke(key); manager.OnReduceNode?.Invoke(key);
        Require(probe.Added == 1 && probe.Removed == 1, "released dynamic node receives no further manager callbacks");
        var original = new Zjcs.Official.HomeTipDots.TipDotLinkTreasureElementItemNode {
            Elements = new System.Collections.Generic.List<Zjcs.Official.BattleSkillPlayables.ElementType>(),
            mNodeSelector = new TipDotGraphNodeSelector() };
        var clone = (Zjcs.Official.HomeTipDots.TipDotLinkTreasureElementItemNode)original.Clone();
        Require(!ReferenceEquals(original.Elements, clone.Elements) && !ReferenceEquals(original.mNodeSelector, clone.mNodeSelector),
            "original element-link clones own independent selector and element collections");
    }
}
