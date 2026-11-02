using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Official.ProfessionUI;

public static class PartnerMainPlayerValidation
{
    static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }
    public static void Run()
    {
        try { Validate(); Debug.Log("PARTNER_MAIN_PLAYER_PASSED"); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
    static void Validate()
    {
        string repo = Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT");
        string work = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
        Require(File.Exists(Path.Combine(work, ".zjcs-validation.json")), "Owned validation profiles required");
        Func<string, string> read = path => File.ReadAllText(Path.Combine(repo, "unity/Assets/Official", path));
        var catalog = OfficialCharacterCatalog.Parse(read("Character/character-progression.json"),
            read("Character/item-time-limits.json"), read("CharacterCreation/wardrobe.json"),
            read("CharacterCreation/wardrobe-rules.json"), read("Character/character-overview.json"));
        var config = Resources.Load<AppearanceConfig>("Combat/SceneAppearance/AppearanceConfig");
        var rules = Resources.Load<AppearanceAnimNameCombineRule>("Combat/SceneAppearance/AppearanceAnimNameCombineRule");
        var tables = ProfessionAppearanceTables.Load(Resources.Load<TextAsset>("Combat/SceneAppearance/appearance-tables").text);
        var defaults = new ProfessionAppearanceSkinDefaults(tables,
            Resources.Load<TextAsset>("Combat/SceneAppearance/skin-defaults").text, message => { throw new Exception(message); });
        var slots = new ProfileSlotCatalog(Path.Combine(work, "profiles"));
        string slot = slots.CreateSlot();
        var session = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
        Require(session.ApplyCharacterMutation(player => {
            player.CharacterCreated = true; player.CharacterName = "名字不是翻译键";
            player.InitialWeaponType = 200; player.SubRank = 1;
            SetAppearance(player, config, tables, Sex.Male, 1);
        }).IsApplied, "Create actual isolated save");
        session.EnsureLocalActorIdentities();
        long id = session.Save.Player.CharacterProperties.LocalActorIdentities.PlayerId;
        var progression = new OfflineCharacterProgressionService(session, catalog);
        var appearance = new OfflineProfessionAppearanceBindings(id, () => progression.Player, item => catalog.ItemType(item),
            tables, config, rules, defaults);
        const long now = 1793667600;
        bool iconFailure = false, assist = false;
        int assistPlay = -1; long assistId = -1;
        var source = new OfflinePartnerPlayerInfoSource(session, progression, appearance, () => now,
            _ => iconFailure ? throw new InvalidOperationException("Injected icon read failure") : null,
            sex => "original-figure-" + sex,
            (play, actor) => { assistPlay = play; assistId = actor; return assist; });
        var original = source.MainPlayer;
        var before = source.BuildData(original);
        Require(before.Id == id && id > 0 && before.TargetData.ServerId == -1 && !before.TargetData.IsNpc
            && before.TargetData.TargetType == ActorTargetType.Player, "Stable real player target and inherited server ID");
        Require(before.Name == "名字不是翻译键" && before.NameArg.ToString() == before.Name && before.Icon == null,
            "Original plain player name and nullable icon");
        Require(before.Level == progression.Player.Level && before.SubRank == progression.Player.SubRank
            && before.CombatRating == progression.EvaluateProps(now).CombatRating && before.CombatRating > 0,
            "Actual configured progression and property calculation");
        Require(before.EngagePetClassId == 0 && before.Sex == Sex.Male && before.Profession == 1,
            "Real initial appearance and absent follower");
        Require(before.TargetData.GetFigurePath(Sex.Female) == "original-figure-Female"
            && !before.TargetData.CheckActorCanAssist(4) && assistPlay == 4 && assistId == id,
            "Original target delegates actual figure and assistance eligibility");
        assist = true;
        Require(before.TargetData.CheckActorCanAssist(7), "Assistance eligibility is live, never a fixed fixture flag");
        int notifications = 0;
        source.OnPlayerUpdate.AddListener(() => {
            notifications++;
            Require(source.BuildData(source.MainPlayer).Name == session.Save.Player.CharacterName,
                "Complete committed generation exists before original manager notification");
        });
        Require(session.ApplyCharacterMutation(player => {
            player.CharacterName = "存档改名"; player.Experience = 100000;
            SetAppearance(player, config, tables, Sex.Female, 10);
            var follower = OfflinePetProgress.AddPet(player.Pets, 91001, 1);
            var other = OfflinePetProgress.AddPet(player.Pets, 91002, 1);
            follower.Status = 4; other.Status = 2;
            player.Pets.FollowPetIndex = follower.Index;
            player.Skills.Plans[0].PetIndex = other.Index;
        }).IsApplied, "Commit profile changes, follower differs from skill-plan pet");
        source.SynchronizeCommittedPlayer();
        var after = source.BuildData(source.MainPlayer);
        Require(notifications == 1 && !ReferenceEquals(original, source.MainPlayer)
            && before.Name == "名字不是翻译键" && original.Name == before.Name,
            "Replace generation without mutating the old original subscriber");
        Require(after.Name == "存档改名" && after.Id == before.Id && after.EngagePetClassId == 91001
            && after.Sex == Sex.Female && after.Profession == 10,
            "Committed identity, following pet, sex and profession reach original data");
        Require(after.CombatRating == progression.EvaluateProps(now).CombatRating
            && after.Level == progression.Player.Level && after.Level > before.Level,
            "Changed real level and recalculated combat rating");
        Require(!ReferenceEquals(before.AppearanceData, after.AppearanceData)
            && ((AppearanceData)before.AppearanceData).SkinMap.Count > 0,
            "New appearance generation preserves old protocol object");
        byte[] saved = File.ReadAllBytes(slots.GetProgressPath(slot));
        var last = source.MainPlayer;
        iconFailure = true;
        try { source.SynchronizeCommittedPlayer(); throw new Exception("Expected snapshot failure"); }
        catch (InvalidOperationException error) { Require(error.Message == "Injected icon read failure", "Expected failure"); }
        Require(ReferenceEquals(last, source.MainPlayer) && notifications == 1
            && saved.SequenceEqual(File.ReadAllBytes(slots.GetProgressPath(slot))),
            "Failed capture publishes nothing and never writes gameplay save");
        var reopened = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
        var reopenedProgression = new OfflineCharacterProgressionService(reopened, catalog);
        var reopenedAppearance = new OfflineProfessionAppearanceBindings(id, () => reopenedProgression.Player,
            item => catalog.ItemType(item), tables, config, rules, defaults);
        var restored = new OfflinePartnerPlayerInfoSource(reopened, reopenedProgression, reopenedAppearance,
            () => now, _ => null, _ => null, (_, __) => false);
        var restoredData = restored.BuildData(restored.MainPlayer);
        Require(restoredData.Id == id && restoredData.Name == after.Name && restoredData.EngagePetClassId == after.EngagePetClassId
            && restoredData.Level == after.Level && restoredData.CombatRating == after.CombatRating,
            "Real profile reopen restores projected identity, level, follower and calculated rating");
        try { source.BuildData(restored.MainPlayer); throw new Exception("Expected foreign profile rejection"); }
        catch (ArgumentException) { }
        Debug.Log("Actual local actor " + id + ", level " + after.Level + ", CP " + after.CombatRating + "; full save reopen and failed capture checked");
    }
    static void SetAppearance(OfflinePlayerProgress player, AppearanceConfig config, ProfessionAppearanceTables tables, Sex sex, int profession)
    {
        var data = new ProfessionAppearanceResolver(tables).BuildDefaultAppearanceData(config, sex, (Profession)profession);
        player.Wardrobe.Sex = (int)sex; player.Wardrobe.Profession = profession;
        player.Wardrobe.BaseSkins = data.SkinMap.Select(pair => new OfflineAppearanceSkin { Type = (int)pair.Key, Id = pair.Value.SkinId }).ToList();
        player.Wardrobe.Dyes = data.DyeMap.Select(pair => new OfflineAppearanceDye { Type = (int)pair.Key,
            ColorId = pair.Value.ColorId, Hue = pair.Value.Hsv.H, Saturation = pair.Value.Hsv.S, Value = pair.Value.Hsv.V }).ToList();
    }
}
