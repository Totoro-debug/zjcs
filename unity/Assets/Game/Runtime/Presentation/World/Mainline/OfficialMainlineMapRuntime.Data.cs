using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Zjcs.Offline.Equipment;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Offline.Skills;
using Zjcs.Offline.State;
using Zjcs.Offline.World;
using Zjcs.Presentation.Combat;
using Zjcs.Presentation.Character;
using Zjcs.Presentation.Commerce;
using Zjcs.Presentation.OfflineWorld;
using Zjcs.Official.World;
using Zjcs.Official.WorldUI;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        private void LoadOfficialData()
        {
            TextAsset battleData = Resources.Load<TextAsset>(MapResource("battle-runtime"));
            TextAsset storyData = Resources.Load<TextAsset>(MapResource("stories"));
            if (battleData == null || storyData == null)
                throw new InvalidOperationException("Official Map" + mapClassId + " battle and story payloads are missing from Resources.");

            Map10RuntimeBattleData battles = JsonUtility.FromJson<Map10RuntimeBattleData>(battleData.text);
            Map10RuntimeStoryData storyDataValue = JsonUtility.FromJson<Map10RuntimeStoryData>(storyData.text);
            puzzleRules = storyDataValue?.puzzleRules;
            statues.witchInfos = storyDataValue?.witchInfos ?? Array.Empty<Map11RuntimeWitchInfo>();
            statues.witchResults = storyDataValue?.witchResultEffects ?? Array.Empty<Map11RuntimeWitchResult>();
            statues.witchPeriodicAwards = storyDataValue?.witchPeriodicityAwards ?? Array.Empty<Map11RuntimePeriodicityAward>();
            hud.quickBuys = storyDataValue?.quickBuys ?? Array.Empty<Map11RuntimeQuickBuy>();
            int expectedStages = MapProfile.StageCount;
            int expectedStories = MapProfile.StoryCount;
            if (battles == null || battles.manifest != "854_156383" || battles.mapClassId != mapClassId
                || battles.stages == null || battles.monsterRosters == null || battles.battleScenes == null
                || battles.stages.Length != expectedStages || battles.monsterRosters.Length != expectedStages
                || (mapClassId == 11 && (battles.entityStageBindings == null || battles.entityStageBindings.Length != 591))
                || storyDataValue == null || storyDataValue.manifest != "854_156383"
                || storyDataValue.mapClassId != mapClassId || storyDataValue.stories == null
                || storyDataValue.dialogRoles == null || storyDataValue.mapLogicTags == null
                || storyDataValue.mapTalkEntries == null || storyDataValue.mapTalkConditions == null
                || storyDataValue.storyOptions == null || storyDataValue.nativeDialogs == null
                || storyDataValue.playables == null
                || (mapClassId == 11 && (storyDataValue.fixedRewards == null
                    || storyDataValue.mainlineTasks == null || storyDataValue.mainlineTasks.Length != 24
                    || storyDataValue.entityTemplates == null || storyDataValue.entityTemplates.Length != 110
                    || storyDataValue.witchInfos == null || storyDataValue.witchInfos.Length != 14)))
                throw new InvalidDataException("Official Map" + mapClassId + " runtime data does not match the pinned source.");

            foreach (Map10RuntimeStage value in battles.stages) stages.Add(value.Id, value);
            foreach (Map10RuntimeEntityTemplate template in storyDataValue.entityTemplates
                         ?? Array.Empty<Map10RuntimeEntityTemplate>())
            {
                if (template == null || template.EntityClassId <= 0 || template.Components == null
                    || template.Components.Length == 0 || entityTemplates.ContainsKey(template.EntityClassId)
                    || template.Components.Any(value => value == null || string.IsNullOrEmpty(value.Type)
                        || string.IsNullOrEmpty(value.Json))
                    || template.Components.Select(value => value.Type).Distinct().Count() != template.Components.Length)
                    throw new InvalidDataException("Official Map" + mapClassId + " EC entity template is malformed.");
                entityTemplates.Add(template.EntityClassId, template);
            }
            foreach (Map10RuntimeRoster value in battles.monsterRosters) rosters.Add(value.StageId, value);
            foreach (Map10RuntimeBattleScene value in battles.battleScenes) battleScenes.Add(value.GameId, value);
            foreach (Map10RuntimeBattleEntityStageBinding value in battles.entityStageBindings ??
                         Array.Empty<Map10RuntimeBattleEntityStageBinding>())
            {
                if (value == null || value.PlacementId <= 0 || value.StageComponents == null
                    || value.StageComponents.Length == 0 || battleEntityStageBindings.ContainsKey(value.PlacementId))
                    throw new InvalidDataException("Official Map" + mapClassId + " battle entity stage binding is malformed.");
                battleEntityStageBindings.Add(value.PlacementId, value);
            }
            foreach (Map10RuntimeStory value in storyDataValue.stories)
            {
                if (value == null || string.IsNullOrWhiteSpace(value.key) || value.nodes == null
                    || value.nodes.Length == 0 || value.entryNodeId <= 0 || value.dialogueGroups == null)
                    throw new InvalidDataException("Official Map" + mapClassId + " story graph is malformed.");
                stories.Add(value.key, value);
            }
            Map10RuntimeStory[] subgraphs = storyDataValue.subgraphs ?? Array.Empty<Map10RuntimeStory>();
            if ((mapClassId == 10 && subgraphs.Length != 0) || (mapClassId == 11 && subgraphs.Length != 4))
                throw new InvalidDataException("Official Map" + mapClassId + " story subgraph closure is incomplete.");
            foreach (Map10RuntimeStory value in subgraphs)
            {
                if (value == null || string.IsNullOrWhiteSpace(value.key) || value.nodes == null
                    || value.nodes.Length == 0 || value.entryNodeId <= 0 || value.dialogueGroups == null
                    || storySubgraphs.ContainsKey(value.key))
                    throw new InvalidDataException("Official Map" + mapClassId + " story subgraph is malformed.");
                storySubgraphs.Add(value.key, value);
            }
            foreach (Map10RuntimeFixedReward value in storyDataValue.fixedRewards ?? Array.Empty<Map10RuntimeFixedReward>())
            {
                if (value == null || value.Id <= 0 || value.CanGetTimes < 0 || value.Items == null
                    || value.Items.Length == 0 || fixedRewards.ContainsKey(value.Id)
                    || value.Items.Any(item => item == null || item.ItemId <= 0 || item.Count <= 0))
                    throw new InvalidDataException("Official Map" + mapClassId + " fixed reward is malformed.");
                fixedRewards.Add(value.Id, value);
            }
            foreach (Map10RuntimeDialogRole value in storyDataValue.dialogRoles)
            {
                if (value == null || value.RoleId <= 0 || value.EntityClassId < 0
                    || string.IsNullOrWhiteSpace(value.RoleName)
                    || (value.EntityClassId > 0 && string.IsNullOrWhiteSpace(value.PortraitPath))
                    || (value.EntityClassId == 0 && value.RoleId != 100000))
                    throw new InvalidDataException("Official Map" + mapClassId + " dialogue role is malformed.");
                dialogRoles.Add(value.RoleId, value);
            }
            if (mapClassId == 10 && (dialogRoles.Count != 4 || !dialogRoles.ContainsKey(840200)
                || !dialogRoles.ContainsKey(840401) || !dialogRoles.ContainsKey(840402)
                || !dialogRoles.ContainsKey(841700)))
                throw new InvalidDataException("Official Map10 dialogue roles are incomplete.");
            foreach (Map10RuntimeNativeDialog value in storyDataValue.nativeDialogs)
            {
                if (value == null || value.Id <= 0 || value.EntryNodeId <= 0 || value.Nodes == null
                    || value.Nodes.Length == 0 || nativeDialogs.ContainsKey(value.Id))
                    throw new InvalidDataException("Official Map" + mapClassId + " native dialog is malformed.");
                foreach (Map10RuntimeNativeDialogNode node in value.Nodes)
                {
                    if (node == null || node.NodeId <= 0 || node.NextNodes == null
                        || (node.NodeType == 0 && (string.IsNullOrEmpty(node.Text) || !dialogRoles.ContainsKey(node.RoleId)))
                        || (node.NodeType == 1 && string.IsNullOrEmpty(node.Text))
                        || (node.ActionType == 4 && (node.AwardItems == null || node.AwardItems.Length == 0)))
                        throw new InvalidDataException("Official Map" + mapClassId + " native dialog node is malformed: " + value.Id);
                }
                nativeDialogs.Add(value.Id, value);
            }
            foreach (Map10RuntimePlayable value in storyDataValue.playables)
            {
                if (value == null || string.IsNullOrWhiteSpace(value.AssetGuid) || value.Duration < -1f
                    || (mapClassId == 10 && value.Duration <= 0f)
                    || officialPlayables.ContainsKey(value.AssetGuid))
                    throw new InvalidDataException("Official Map" + mapClassId + " playable binding is malformed.");
                officialPlayables.Add(value.AssetGuid, value);
                foreach (string key in value.Keys ?? Array.Empty<string>())
                {
                    if (string.IsNullOrWhiteSpace(key) || officialPlayables.ContainsKey(key))
                        throw new InvalidDataException("Official Map" + mapClassId + " playable key is duplicated or empty.");
                    officialPlayables.Add(key, value);
                }
            }
            if (mapClassId == 10 && (!officialPlayables.ContainsKey("FeiLe_appear")
                || !officialPlayables.ContainsKey("FeiLe_disappear")
                || !officialPlayables.ContainsKey("9b248cde91299ee4287533b25b7dd468")))
                throw new InvalidDataException("Official Map10 story playable bindings are incomplete.");
            foreach (Map10RuntimeStory story in stories.Values)
                foreach (Map10RuntimeStoryNode node in story.nodes.Where(value => value != null
                             && value.type == "PlayPlayableNode"))
                    if ((node.playableDuration <= 0f && node.waitPlayableStop)
                        || !officialPlayables.ContainsKey(node.playableAssetGuid))
                        throw new InvalidDataException("Official Map" + mapClassId + " story playable data is incomplete: " + story.key);
            foreach (Map10RuntimeLogicTag value in storyDataValue.mapLogicTags)
            {
                if (value == null || value.MapClass != mapClassId || value.LogicId <= 0
                    || value.EntityClass <= 0 || string.IsNullOrWhiteSpace(value.Tag))
                    throw new InvalidDataException("Official Map" + mapClassId + " logic entity tag is malformed.");
                mapLogicTags.Add(value.LogicId, value);
            }
            foreach (Map10RuntimeTalkConditionInfo value in storyDataValue.mapTalkConditions)
            {
                if (value == null || value.ClassId <= 0 || value.Condition == null
                    || mapTalkConditions.ContainsKey(value.ClassId))
                    throw new InvalidDataException("Official Map10 MapTalk condition is malformed.");
                mapTalkConditions.Add(value.ClassId, value.Condition);
            }
            mapTalkEntries = storyDataValue.mapTalkEntries;
            foreach (Map10RuntimeStoryOption value in storyDataValue.storyOptions)
            {
                if (value == null || string.IsNullOrWhiteSpace(value.StoryName) || value.Id < 0
                    || string.IsNullOrWhiteSpace(value.Text))
                    throw new InvalidDataException("Official Map10 story option is malformed.");
                storyOptions.Add(StoryOptionKey(value.StoryName, value.Id), value);
            }
            if (mapClassId == 10)
            {
                string[] requiredStories =
                {
                    "FirstEnterMap", "FirstBattle", "FirstMeetChaos", "SavingChick",
                    "ChickYell", "ChickYell2", "ChickChat", "FangjianbeiActive", "UnlockFangBei"
                };
                if (stories.Count != requiredStories.Length || requiredStories.Any(name => !stories.ContainsKey(name)))
                    throw new InvalidDataException("Official Map10 story payload is incomplete.");
            }
            else if (stories.Count != expectedStories || !stories.ContainsKey("FirstMeetSenSen")
                || !stories.ContainsKey("CureWorldTree") || !stories.ContainsKey("GriffonTogether"))
                throw new InvalidDataException("Official Map11 story payload is incomplete.");
            if (mapClassId == 11 && (!storySubgraphs.ContainsKey("FangjianbeiActive")
                || !storySubgraphs.ContainsKey("GriffonTeamUp") || !storySubgraphs.ContainsKey("CombatAssistance")
                || !storySubgraphs.ContainsKey("HolyMonsterActive") || fixedRewards.Count != 8))
                throw new InvalidDataException("Official Map11 story support data is incomplete.");
        }
    }
}
