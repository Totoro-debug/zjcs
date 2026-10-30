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
            if (mapClassId == 15)
            {
                // Map15 currently uses the verified official group/entity closure and the
                // shared navigation host. Its battle/story payloads are not yet wired to
                // this host, so keep all optional runtime collections empty explicitly.
                puzzleRules = Array.Empty<MapPuzzleRule>();
                statues.witchInfos = Array.Empty<Map11RuntimeWitchInfo>();
                statues.witchResults = Array.Empty<Map11RuntimeWitchResult>();
                statues.witchPeriodicAwards = Array.Empty<Map11RuntimePeriodicityAward>();
                hud.quickBuys = Array.Empty<Map11RuntimeQuickBuy>();
                mapTalkEntries = Array.Empty<Map10RuntimeMapTalkEntry>();
                return;
            }
            TextAsset battleData = Resources.Load<TextAsset>(MapResource("battle-runtime"));
            TextAsset storyData = Resources.Load<TextAsset>(MapResource("stories"));
            if (battleData == null || storyData == null)
                throw new InvalidOperationException("Official Map" + mapClassId + " battle and story payloads are missing from Resources.");

            Map10RuntimeBattleData battles = JsonUtility.FromJson<Map10RuntimeBattleData>(battleData.text);
            Map10RuntimeStoryData storyDataValue = JsonUtility.FromJson<Map10RuntimeStoryData>(storyData.text);
            string expectedBattleManifest = mapClassId == 14 ? "871_156598" : "854_156383";
            string expectedStoryManifest = mapClassId >= 13 ? "871_156598" : "854_156383";
            puzzleRules = storyDataValue?.puzzleRules;
            statues.witchInfos = storyDataValue?.witchInfos ?? Array.Empty<Map11RuntimeWitchInfo>();
            statues.witchResults = storyDataValue?.witchResultEffects ?? Array.Empty<Map11RuntimeWitchResult>();
            statues.witchPeriodicAwards = storyDataValue?.witchPeriodicityAwards ?? Array.Empty<Map11RuntimePeriodicityAward>();
            hud.quickBuys = storyDataValue?.quickBuys ?? Array.Empty<Map11RuntimeQuickBuy>();
            if (mapClassId == 13)
            {
                Map11RuntimeQuickBuy quote = hud.quickBuys.SingleOrDefault(value => value.ItemId == 325);
                if (hud.quickBuys.Length != 1 || quote == null || quote.CurrencyId != 2 || quote.BaseCount != 1
                    || quote.RefreshType != 0 || quote.SubRankScaleId != 0 || quote.LevelScaleId != 0
                    || quote.PriceList == null || quote.PriceList.Length != 1 || quote.PriceList[0] != 50
                    || quote.PriceBuyCount == null || quote.PriceBuyCount.Length != 1 || quote.PriceBuyCount[0] != -1
                    || quote.Condition == null || quote.Condition._type != "HasItemCondition"
                    || quote.Condition.ClassId != 4013 || quote.Condition.Count != 1 || quote.Condition.Op != ">=")
                    throw new InvalidDataException("Official Map13 item 325 quick-buy quote differs from the pinned source.");
            }
            int expectedStages = MapProfile.StageCount;
            int expectedStories = MapProfile.StoryCount;
            if (battles == null || battles.manifest != expectedBattleManifest || battles.mapClassId != mapClassId
                || battles.stages == null || battles.monsterRosters == null || battles.battleScenes == null
                || battles.stages.Length != expectedStages || battles.monsterRosters.Length != expectedStages
                || (mapClassId == 11 && (battles.entityStageBindings == null || battles.entityStageBindings.Length != 591))
                || (mapClassId == 12 && (battles.entityStageBindings == null || battles.entityStageBindings.Length != 858))
                || (mapClassId == 14 && (battles.entityStageBindings == null || battles.entityStageBindings.Length != 909
                    || battles.battleScenes.Length != 20))
                || storyDataValue == null || storyDataValue.manifest != expectedStoryManifest
                || storyDataValue.mapClassId != mapClassId || storyDataValue.stories == null
                || storyDataValue.dialogRoles == null || storyDataValue.mapLogicTags == null
                || storyDataValue.mapTalkEntries == null || storyDataValue.mapTalkConditions == null
                || storyDataValue.storyOptions == null || storyDataValue.nativeDialogs == null
                || storyDataValue.playables == null
                || (mapClassId == 13 && storyDataValue.playerConditions == null)
                || (mapClassId == 11 && (storyDataValue.fixedRewards == null
                    || storyDataValue.mainlineTasks == null || storyDataValue.mainlineTasks.Length != 24
                    || storyDataValue.entityTemplates == null || storyDataValue.entityTemplates.Length != 110
                    || storyDataValue.witchInfos == null || storyDataValue.witchInfos.Length != 14))
                || (mapClassId == 12 && (storyDataValue.fixedRewards == null
                    || storyDataValue.mainlineTasks == null || storyDataValue.mainlineTasks.Length != 22
                    || storyDataValue.entityTemplates == null || storyDataValue.entityTemplates.Length != 135
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
            if ((mapClassId == 10 && subgraphs.Length != 0) || (mapClassId == 11 && subgraphs.Length != 4)
                || (mapClassId == 12 && subgraphs.Length != 17) || (mapClassId == 14 && subgraphs.Length != 21))
                throw new InvalidDataException("Official Map" + mapClassId + " story subgraph closure is incomplete.");
            foreach (Map10RuntimeStory value in subgraphs)
            {
                if (value == null || string.IsNullOrWhiteSpace(value.key) || value.nodes == null
                    || value.nodes.Length == 0 || value.entryNodeId <= 0 || value.dialogueGroups == null
                    || storySubgraphs.ContainsKey(value.key))
                    throw new InvalidDataException("Official Map" + mapClassId + " story subgraph is malformed.");
                storySubgraphs.Add(value.key, value);
            }
            // Some original Map13 graphs are both subgraph targets and direct MapTalk entries.
            // Keep one payload per graph while exposing those entries to the root dispatcher.
            if (mapClassId == 13)
                foreach (Map10RuntimeMapTalkEntry talk in storyDataValue.mapTalkEntries)
                {
                    if (talk == null || string.IsNullOrEmpty(talk.TalkId) || stories.ContainsKey(talk.TalkId)) continue;
                    if (!storySubgraphs.TryGetValue(talk.TalkId, out Map10RuntimeStory graph))
                        throw new InvalidDataException("Official Map13 MapTalk graph is missing: " + talk.TalkId);
                    stories.Add(talk.TalkId, graph);
                }
            foreach (Map10RuntimeFixedReward value in storyDataValue.fixedRewards ?? Array.Empty<Map10RuntimeFixedReward>())
            {
                if (value == null || value.Id <= 0 || value.CanGetTimes < 0 || value.Items == null
                    || value.Items.Length == 0 || fixedRewards.ContainsKey(value.Id)
                    || value.Items.Any(item => item == null || item.ItemId <= 0 || item.Count <= 0))
                    throw new InvalidDataException("Official Map" + mapClassId + " fixed reward is malformed.");
                fixedRewards.Add(value.Id, value);
            }
            foreach (Map13RuntimeHandInItem value in storyDataValue.handInItems ?? Array.Empty<Map13RuntimeHandInItem>())
            {
                if (value == null || value.Id <= 0 || value.CanHandTimes < 0 || value.Items == null
                    || value.Items.Length == 0 || handInItems.ContainsKey(value.Id)
                    || value.Items.Any(item => item == null || item.ItemId <= 0 || item.Count <= 0))
                    throw new InvalidDataException("Official Map" + mapClassId + " hand-in item configuration is malformed.");
                handInItems.Add(value.Id, value);
            }
            if (mapClassId == 14 && (handInItems.Count != 1
                || !handInItems.TryGetValue(4, out Map13RuntimeHandInItem map14HandIn)
                || map14HandIn.CanHandTimes != 1 || map14HandIn.Items.Length != 1
                || map14HandIn.Items[0].ItemId != 2208 || map14HandIn.Items[0].Count != 1))
                throw new InvalidDataException("Official Map14 hand-in configuration does not match the pinned source.");
            if (mapClassId == 14 && storyDataValue.stories.Concat(storyDataValue.subgraphs ?? Array.Empty<Map10RuntimeStory>())
                .SelectMany(value => value.nodes)
                .Where(value => value.type == "HandInItemNode" || value.type == "CanHandInItemNode")
                .Any(value => !handInItems.ContainsKey(value.handInItemId)))
                throw new InvalidDataException("Official Map14 story references an unknown hand-in configuration.");
            foreach (Map10RuntimeDialogRole value in storyDataValue.dialogRoles)
            {
                if (value == null || value.RoleId <= 0 || value.EntityClassId < 0
                    || string.IsNullOrWhiteSpace(value.RoleName)
                    || (mapClassId != 13 && value.EntityClassId > 0 && string.IsNullOrWhiteSpace(value.PortraitPath))
                    || (value.EntityClassId == 0 && value.RoleId != 100000
                        && !(mapClassId == 14 && value.RoleId == 100004)))
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
            if (mapClassId == 13) ValidateMap13StoryPlayables();
            foreach (Map10RuntimeStory story in stories.Values.Where(_ => mapClassId != 13 && mapClassId != 14))
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
            if (mapClassId == 13)
            {
                map13PlayerConditionEvaluator = new Map13OfflinePlayerConditionEvaluator(storyDataValue.playerConditions,
                    StoryItemCount, GetStoryPlayCount, key => progressionSave.StoryStatus(key),
                    id => progressionSave.StageWinCount(id), Map13RegionProgress, IsMap13StoryTagCompleted);
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
            else if (mapClassId == 11 && (stories.Count != expectedStories || !stories.ContainsKey("FirstMeetSenSen")
                || !stories.ContainsKey("CureWorldTree") || !stories.ContainsKey("GriffonTogether")))
                throw new InvalidDataException("Official Map11 story payload is incomplete.");
            else if (mapClassId == 12 && (stories.Count != expectedStories || !stories.ContainsKey("FirstMeetMete")
                || !stories.ContainsKey("PhoenixPlaying") || !stories.ContainsKey("FangjianbeiActive_map12")
                || !storySubgraphs.ContainsKey("FangjianbeiActive") || !storySubgraphs.ContainsKey("ChaosAfterBattle")
                || fixedRewards.Count != 12))
                throw new InvalidDataException("Official Map12 story support data is incomplete.");
            else if (mapClassId == 14 && (expectedStories != 85 || stories.Count != 73
                || stories.Count + storySubgraphs.Count != 94 || storySubgraphs.Count != 21
                || storyDataValue.entityTemplates == null || storyDataValue.entityTemplates.Length != 171
                || storyDataValue.dialogRoles.Length != 16 || storyDataValue.mapLogicTags.Length != 60
                || storyDataValue.mapTalkEntries.Length != 79 || storyDataValue.mapTalkConditions.Length != 18
                || fixedRewards.Count != 9 || statues.witchInfos.Length != 14
                || handInItems.Count != 1 || !handInItems.ContainsKey(4)))
                throw new InvalidDataException("Official Map14 story support data is incomplete.");
            if (mapClassId == 11 && (!storySubgraphs.ContainsKey("FangjianbeiActive")
                || !storySubgraphs.ContainsKey("GriffonTeamUp") || !storySubgraphs.ContainsKey("CombatAssistance")
                || !storySubgraphs.ContainsKey("HolyMonsterActive") || fixedRewards.Count != 8))
                throw new InvalidDataException("Official Map11 story support data is incomplete.");
        }

        private int Map13RegionProgress(int map, int region) => map == 13
            ? progressionSave.Save.World.Maps.FirstOrDefault(value => value.MapClassId == map)?.Regions
                .FirstOrDefault(value => value.Region == region)?.Progress ?? 0
            : 0;

        private bool IsMap13StoryTagCompleted(string tag) => mapClassId == 13 &&
            (progressionSave.Save.World.CompletedEntityTags.Contains(tag)
                || localSave.mapLogicStates.Any(value => value != null && value.Tag == tag && value.State < 0));
    }
}
