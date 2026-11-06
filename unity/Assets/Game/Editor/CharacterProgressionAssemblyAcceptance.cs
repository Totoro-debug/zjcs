using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.Equipment;
using Zjcs.Offline.Progression;
using Zjcs.Offline.Skills;
using Zjcs.Offline.State;

namespace Zjcs.Editor
{
    public static class CharacterProgressionAssemblyAcceptance
    {
        public static void Run()
        {
            try
            {
                var root = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
                if (string.IsNullOrWhiteSpace(root)) throw new InvalidOperationException("An isolated validation root is required.");
                var slots = new ProfileSlotCatalog(Path.Combine(root, "assembly-profiles"));
                string slot = slots.CreateSlot();
                var profile = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
                var characterAssets = OfficialCharacterAssetCatalog.LoadDefault();
                var character = characterAssets.Load();
                var equipment = OfficialEquipmentAssetCatalog.LoadDefault().Load();
                var skills = characterAssets.LoadSkillRules();
                const long now = 1700000000;
                var live = OfflineCharacterProgressionService.Create(profile,
                    new CharacterProgressionInputs(character, equipment, skills));
                foreach (string state in new[] { "initial", "equipment", "skills", "reload" })
                {
                    if (state == "equipment")
                    {
                        var changed = profile.ApplyCharacterMutation(player => {
                            player.Level = 40; player.Experience = character.CumulativeLevelExperience[40];
                            player.InitialWeaponType = 200; player.Wardrobe.Profession = 1;
                            var item = OfficialEquipmentGenerator.Generate(equipment, 20000, player.Equipment.NextInstanceId++,
                                new EquipmentRollContext { DropLevel = 1, PlayerLevel = 40, SubRank = 0 }, () => .5f);
                            player.Equipment.Instances.Add(item);
                        }, "assembly:equipment");
                        if (!changed.IsApplied) throw new InvalidOperationException(changed.Message);
                        var worn = live.WearEquipment(equipment, profile.Save.Player.Equipment.Instances.Last().InstanceId, "assembly:wear");
                        if (!worn.IsApplied) throw new InvalidOperationException(worn.Message);
                    }
                    if (state == "skills")
                    {
                        var owned = profile.ApplyCharacterMutation(player => player.Skills.Skills.Add(
                            new OfflineOwnedSkill { ClassId = 10014, Rank = 1 }), "assembly:skill-owned");
                        if (!owned.IsApplied) throw new InvalidOperationException(owned.Message);
                        var equipped = live.EquipSkill(10014, 0);
                        if (!equipped.IsApplied) throw new InvalidOperationException(equipped.Message);
                    }
                    if (state == "reload") profile.Reload();
                    var beforeHome = new OfflineCharacterProgressionService(profile, character);
                    beforeHome.AttachEquipmentCatalog(equipment); beforeHome.AttachSkillRules(skills);
                    var beforeMap = new OfflineCharacterProgressionService(profile, character);
                    beforeMap.AttachSkillRules(skills); beforeMap.AttachEquipmentCatalog(equipment);
                    var assembled = OfflineCharacterProgressionService.Create(profile,
                        new CharacterProgressionInputs(character, equipment, skills));
                    string expected = Snapshot(beforeHome.EvaluateProps(now));
                    foreach (var candidate in new[] { beforeMap, assembled, live })
                        if (Snapshot(candidate.EvaluateProps(now)) != expected)
                            throw new InvalidOperationException("Full property pipeline/MaxHp/combat rating diverged: " + state);
                }
                foreach (var missing in new[] {
                    new CharacterProgressionInputs(null, equipment, skills),
                    new CharacterProgressionInputs(character, null, skills),
                    new CharacterProgressionInputs(character, equipment, null) })
                {
                    try { OfflineCharacterProgressionService.Create(profile, missing); }
                    catch (CharacterProgressionInitializationException error)
                    { if (string.IsNullOrEmpty(error.Stage)) throw; continue; }
                    throw new InvalidOperationException("Incomplete progression inputs escaped initialization.");
                }
                ProjectTools.Validate();
                Debug.Log("Character progression assembly passed: both historical attachment orders, full property chunks, MaxHp, rating, changed profile, reload and missing dependencies.");
                EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }

        private static string Values(IReadOnlyDictionary<int, long> values) => string.Join(",", values.OrderBy(row => row.Key).Select(row => row.Key + ":" + row.Value));
        private static string Chunks(IReadOnlyDictionary<int, IReadOnlyDictionary<int, long>> values) =>
            string.Join(";", values.OrderBy(row => row.Key).Select(row => row.Key + "=[" + Values(row.Value) + "]"));
        private static string Snapshot(CharacterPropSnapshot value) => value.CombatRating + "|" + Values(value.BaseBattleProps)
            + "|" + Values(value.BattleProps) + "|" + Values(value.OutOfFightBattleProps) + "|" + Values(value.NonBattleProps)
            + "|" + Chunks(value.Chunks) + "|" + Chunks(value.DetailProps) + "|" + string.Join(",", value.DetailOrder);
    }
}
