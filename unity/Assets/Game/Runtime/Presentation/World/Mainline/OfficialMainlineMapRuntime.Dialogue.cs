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
        private void PlayMapTalkDialog(Map10RuntimeMapTalkEntry talk, Map10RuntimeEntity entity,
            Action completed = null, bool offerStage = true)
        {
            if (talk == null || !nativeDialogs.TryGetValue(talk.DialogId, out activeNativeDialog))
                throw new InvalidDataException("Official Map10 native dialog is missing: " + (talk == null ? 0 : talk.DialogId));
            nativeDialogContext = entity;
            nativeDialogCompletion = completed ?? (offerStage && entity != null && entity.StageIds.Any(value => value > 0)
                ? (Action)(() => ShowStageDialog(entity, null)) : null);
            nativeDialogNpc = null;
            nativeDialogActive = true;
            dialogueWaiting = true;
            dialogueAdvanceRequested = false;
            RunNativeDialog(activeNativeDialog.EntryNodeId);
        }

        private void RunNativeDialog(int nodeId)
        {
            if (!nativeDialogActive || activeNativeDialog == null) return;
            for (int count = 0; count < 64 && nativeDialogActive; count++)
            {
                Map10RuntimeNativeDialogNode node = activeNativeDialog.Nodes.FirstOrDefault(value =>
                    value != null && value.NodeId == nodeId);
                if (node == null) throw new InvalidDataException("Official Map10 dialog node is missing: " + nodeId);
                nativeDialogNodeId = node.NodeId;

                if (node.NodeType == 0)
                {
                    ShowNativeDialogText(node);
                    return;
                }
                if (node.NodeType == 1)
                {
                    ShowNativeDialogOptions(new[] { node.NodeId });
                    return;
                }
                if (node.NodeType != 2)
                    throw new InvalidDataException("Unsupported official Map10 dialog node type: " + node.NodeType);

                int next = node.NextNodes.FirstOrDefault();
                switch (node.ActionType)
                {
                    case 0:
                        if (node.NextNodes.Length == 0) FinishNativeDialog();
                        else if (node.NextNodes.Length == 1) { nodeId = next; continue; }
                        else { ShowNativeDialogOptions(node.NextNodes); return; }
                        return;
                    case 1:
                        ChangeNativeDialogProfession(node);
                        nodeId = next;
                        continue;
                    case 4:
                        string receipt = MapReceiptPrefix + slotId + ":dialog:" + activeNativeDialog.Id + ":node:" + node.NodeId;
                        if (localSave.nativeDialogAwardReceipts.Contains(receipt))
                        {
                            if (node.AwardLimitedNodeId > 0
                                && activeNativeDialog.Nodes.Any(value => value.NodeId == node.AwardLimitedNodeId))
                            {
                                nodeId = node.AwardLimitedNodeId;
                                continue;
                            }
                        }
                        HideNativeDialogPanel();
                        nativeDialogPlayableWaiting = true;
                        GrantNativeDialogAwards(node, receipt);
                        StartCoroutine(ContinueNativeDialogAfterAward(next));
                        return;
                    case 6:
                        if (string.IsNullOrWhiteSpace(node.Story) || !stories.ContainsKey(node.Story))
                            throw new InvalidDataException("Official Map10 dialog references a missing story: " + node.Story);
                        HideNativeDialogPanel();
                        dialogueWaiting = false;
                        StartStoryGraph(node.Story, nativeDialogContext, () =>
                        {
                            if (!nativeDialogActive) return;
                            dialogueWaiting = true;
                            RunNativeDialog(next);
                        });
                        return;
                    case 10:
                        SpawnNativeDialogNpc(node);
                        HideNativeDialogPanel();
                        nativeDialogPlayableWaiting = true;
                        StartCoroutine(ContinueNativeDialogAfterPlayable(
                            OfficialPlayableDuration(node.PlayableKey), next));
                        return;
                    case 11:
                        HideNativeDialogPanel();
                        dialogueWaiting = true;
                        nativeDialogPlayableWaiting = true;
                        StartCoroutine(FinishNativeDialogAfterDelay(OfficialPlayableDuration(node.PlayableKey)));
                        return;
                    default:
                        throw new InvalidDataException("Unsupported official Map10 dialog action: " + node.ActionType);
                }
            }
            if (nativeDialogActive)
                throw new InvalidDataException("Official Map10 dialog exceeded its node limit: " + activeNativeDialog.Id);
        }

        private void ShowNativeDialogText(Map10RuntimeNativeDialogNode node)
        {
            if (!dialogRoles.TryGetValue(node.RoleId, out Map10RuntimeDialogRole role))
                throw new InvalidDataException("Official Map10 dialog role is missing: " + node.RoleId);
            nativeDialogChoicesWaiting = false;
            nativeDialogChoiceTargets = Array.Empty<int>();
            hud.ui.SetOptionChoicesVisible(false);
            for (int i = 0; i < 5; i++) hud.ui.SetNodeActive("OptionTalkDialog", StoryOptionSlotPath(i), false);
            hud.ui.SetNodeActive("OptionTalkDialog", "/OptionTalkDialog/NextBtn", true);
            hud.ui.SetPanelActive("OptionTalkDialog", true);
            optionDialoguePortraits ??= new OfficialMap10DialoguePortraitView(hud.ui, "OptionTalkDialog", mapClassId);
            ShowDialogueContent("OptionTalkDialog", optionDialoguePortraits, role, node.RoleFaceType, node.Text, null);
            GameObject next = hud.ui.Find("OptionTalkDialog", "/OptionTalkDialog/NextBtn");
            Button button = next == null ? null : next.GetComponent<Button>();
            if (button == null) throw new InvalidDataException("Official Map10 native dialog next button is missing.");
            button.onClick.RemoveListener(RequestDialogueAdvance);
            button.onClick.AddListener(RequestDialogueAdvance);
            dialogueWaiting = true;
            dialogueAdvanceRequested = false;
        }

        private void ShowNativeDialogOptions(IEnumerable<int> nodeIds)
        {
            dialogueTypeWriter?.StopType();
            nativeDialogChoiceTargets = nodeIds.Select(id => activeNativeDialog.Nodes.FirstOrDefault(value =>
                    value != null && value.NodeId == id))
                .Where(value => value != null && value.NodeType == 1)
                .Where(value => value.OptionFilterType != 1
                    || progressionSave.Save.Player.Wardrobe.Profession != value.OptionFilterProfession)
                .Select(value => value.NodeId).Take(5).ToArray();
            if (nativeDialogChoiceTargets.Length == 0)
                throw new InvalidDataException("Official Map10 native dialog has no available choices: " + nativeDialogNodeId);

            nativeDialogChoicesWaiting = true;
            hud.ui.SetOptionChoicesVisible(true);
            dialogueWaiting = true;
            dialogueAdvanceRequested = false;
            hud.ui.SetNodeActive("OptionTalkDialog", "/OptionTalkDialog/NextBtn", false);
            for (int index = 0; index < 5; index++)
            {
                string root = StoryOptionSlotPath(index);
                bool active = index < nativeDialogChoiceTargets.Length;
                hud.ui.SetNodeActive("OptionTalkDialog", root, active);
                if (!active) continue;
                Map10RuntimeNativeDialogNode option = activeNativeDialog.Nodes.First(value =>
                    value.NodeId == nativeDialogChoiceTargets[index]);
                hud.ui.SetText("OptionTalkDialog", root + "/Content/OptionText/Text", option.Text);
                Sprite icon = hud.uiCatalog.FindSprite(option.OptionIconPath);
                if (icon != null) hud.ui.SetSprite("OptionTalkDialog", root + "/Content/TypeImg", icon);
                GameObject item = hud.ui.Find("OptionTalkDialog", root);
                Button button = item == null ? null : item.GetComponent<Button>();
                if (button == null) throw new InvalidDataException("Official Map10 native option button is missing: " + root);
                int capturedIndex = index;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => SelectNativeDialogOption(capturedIndex));
            }
            hud.ui.SetPanelActive("OptionTalkDialog", true);
        }

        private void SelectNativeDialogOption(int index)
        {
            if (!nativeDialogActive || !nativeDialogChoicesWaiting
                || index < 0 || index >= nativeDialogChoiceTargets.Length) return;
            int target = nativeDialogChoiceTargets[index];
            nativeDialogChoicesWaiting = false;
            for (int i = 0; i < 5; i++) hud.ui.SetNodeActive("OptionTalkDialog", StoryOptionSlotPath(i), false);
            hud.ui.SetOptionChoicesVisible(false);
            RunNativeDialog(target);
        }

        private void AdvanceNativeDialog()
        {
            dialogueAdvanceRequested = false;
            Map10RuntimeNativeDialogNode current = activeNativeDialog?.Nodes?.FirstOrDefault(value =>
                value != null && value.NodeId == nativeDialogNodeId);
            if (current == null) throw new InvalidDataException("Official Map10 native dialog lost its current node.");
            if (current.NextNodes == null || current.NextNodes.Length == 0) FinishNativeDialog();
            else if (current.NextNodes.Length == 1) RunNativeDialog(current.NextNodes[0]);
            else ShowNativeDialogOptions(current.NextNodes);
        }

        private void SpawnNativeDialogNpc(Map10RuntimeNativeDialogNode node)
        {
            if (node.NpcEntityClassId != 8402)
                throw new InvalidDataException("Unsupported official Map10 dialog NPC class: " + node.NpcEntityClassId);
            if (nativeDialogNpc != null) entityView.RemoveStoryEntity(nativeDialogNpc);
            MapGridPoint grid = FindAdjacentStoryGrid();
            nativeDialogNpc = entityView.SpawnStoryEntity(node.NpcEntityClassId,
                "Map/Common/EntityDisplaysWorldNPC/WorldNPC_Common/8402_view", new Vector2(grid.X, grid.Y));
            entityView.PlayStoryEntityAnimation(nativeDialogNpc, "appear_01");
        }

        private float OfficialPlayableDuration(string keyOrGuid)
        {
            if (!officialPlayables.TryGetValue(keyOrGuid ?? string.Empty, out Map10RuntimePlayable playable))
                throw new InvalidDataException("Official Map10 playable is not loaded: " + keyOrGuid);
            return playable.Duration;
        }

        private IEnumerator ContinueNativeDialogAfterPlayable(float seconds, int nextNodeId)
        {
            yield return new WaitForSecondsRealtime(seconds);
            if (!nativeDialogActive) yield break;
            nativeDialogPlayableWaiting = false;
            dialogueWaiting = true;
            RunNativeDialog(nextNodeId);
        }

        private IEnumerator ContinueNativeDialogAfterAward(int nextNodeId)
        {
            yield return new WaitForSecondsRealtime(2f);
            hud.HideOfficialNotice();
            if (!nativeDialogActive) yield break;
            nativeDialogPlayableWaiting = false;
            dialogueWaiting = true;
            RunNativeDialog(nextNodeId);
        }

        private void ChangeNativeDialogProfession(Map10RuntimeNativeDialogNode node)
        {
            if (node.Profession != 1 && node.Profession != 10)
                throw new InvalidDataException("Unsupported official Map10 dialog profession: " + node.Profession);
            ProgressionResult result = progressionSave.ApplyCharacterMutation(
                player => player.Wardrobe.Profession = node.Profession,
                MapReceiptPrefix + slotId + ":dialog:" + activeNativeDialog.Id + ":profession:" + node.Profession);
            if (result.Status == ProgressionResultStatus.InvalidState)
                throw new InvalidOperationException(result.Message);
            progression = new OfflineCharacterProgressionService(progressionSave, progression.Catalog);
            hud.ui.SetText("WorldCanvas", "/WorldCanvas/Content/Content/WorldPanel/Anim/Left/PublicPlayerInfo/NameText", characterName);
        }

        private void GrantNativeDialogAwards(Map10RuntimeNativeDialogNode node, string receipt)
        {
            ProgressionResult result = progressionSave.ApplyInventoryTransaction(receipt, save =>
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                foreach (Map10RuntimeAwardItem award in node.AwardItems)
                {
                    InventoryOperationResult granted = OfflineInventoryOperations.Grant(save.Player,
                        award.ItemId, award.Count, now);
                    if (!granted.IsApplied)
                        throw new InvalidOperationException("Official Map10 dialog award could not be applied: " + award.ItemId);
                }
            });
            if (result.Status == ProgressionResultStatus.InvalidState)
                throw new InvalidOperationException(result.Message);
            if (!localSave.nativeDialogAwardReceipts.Contains(receipt))
                localSave.nativeDialogAwardReceipts = localSave.nativeDialogAwardReceipts.Concat(new[] { receipt }).ToArray();
            SaveLocalProgress();
            hud.ShowOfficialNotice("獲得物品 " + string.Join("、", node.AwardItems.Select(value => value.ItemId + " ×" + value.Count)));
        }

        private IEnumerator FinishNativeDialogAfterDelay(float seconds)
        {
            if (nativeDialogNpc != null) entityView.PlayStoryEntityAnimation(nativeDialogNpc, "disappear_01");
            yield return new WaitForSecondsRealtime(seconds);
            if (nativeDialogNpc != null) entityView.RemoveStoryEntity(nativeDialogNpc);
            nativeDialogNpc = null;
            nativeDialogPlayableWaiting = false;
            FinishNativeDialog();
        }

        private void HideNativeDialogPanel()
        {
            hud.ui.SetPanelActive("OptionTalkDialog", false);
            for (int i = 0; i < 5; i++) hud.ui.SetNodeActive("OptionTalkDialog", StoryOptionSlotPath(i), false);
            optionDialoguePortraits?.Clear();
        }

        private void FinishNativeDialog()
        {
            HideNativeDialogPanel();
            if (nativeDialogNpc != null) entityView.RemoveStoryEntity(nativeDialogNpc);
            nativeDialogNpc = null;
            Action completed = nativeDialogCompletion;
            nativeDialogActive = false;
            nativeDialogChoicesWaiting = false;
            nativeDialogPlayableWaiting = false;
            nativeDialogChoiceTargets = Array.Empty<int>();
            activeNativeDialog = null;
            nativeDialogContext = null;
            nativeDialogCompletion = null;
            nativeDialogNodeId = 0;
            dialogueWaiting = false;
            dialogueAdvanceRequested = false;
            completed?.Invoke();
        }

        private void PlayStory(string key, int groupId, Action complete, Map10RuntimeStoryNode graphNode = null)
        {
            if (!stories.TryGetValue(key, out Map10RuntimeStory story) && !storySubgraphs.TryGetValue(key, out story))
                throw new InvalidDataException("Official Map10 story is missing: " + key);
            Map10RuntimeDialogueGroup group = story.dialogueGroups.FirstOrDefault(value => value.Id == groupId);
            if (group == null || group.Lines == null || group.Lines.Length == 0)
            {
                complete?.Invoke();
                return;
            }
            dialogueOverrides = ResolveStoryDialogOverrides(story, graphNode);
            dialoguePanelKey = graphNode?.type == "OptionDialogNode" ? "OptionTalkDialog" : "TalkDialog";
            dialogueVoiceSuppressed = graphNode?.type == "OptionDialogNode" && StoryFields(graphNode).ShowOptionsAtBegin != 0;
            if (dialoguePanelKey == "OptionTalkDialog")
            {
                for (int index = 0; index < 5; index++) hud.ui.SetNodeActive(dialoguePanelKey, StoryOptionSlotPath(index), false);
                hud.ui.SetNodeActive(dialoguePanelKey, "/OptionTalkDialog/NextBtn", true);
            }
            dialogueLines = group.Lines.OrderBy(value => value.Order).ToArray();
            dialogueLineIndex = 0;
            dialogueFinished = complete;
            dialogueWaiting = true;
            dialogueAdvanceRequested = false;
            ShowDialogueLine();
        }

        private void ShowDialogueLine()
        {
            if (dialogueLineIndex >= dialogueLines.Length)
            {
                if (dialoguePanelKey == "TalkDialog")
                {
                    hud.ui.SetPanelActive(dialoguePanelKey, false);
                    mapVoice?.StopVoice();
                    dialoguePortraits?.Clear();
                }
                else hud.ui.SetNodeActive(dialoguePanelKey, "/OptionTalkDialog/NextBtn", false);
                dialogueWaiting = false;
                Action complete = dialogueFinished;
                dialogueFinished = null;
                complete?.Invoke();
                return;
            }
            Map10RuntimeDialogueLine line = dialogueLines[dialogueLineIndex];
            if (!dialogRoles.TryGetValue(line.RoleId, out Map10RuntimeDialogRole role))
                throw new InvalidDataException("Official Map10 dialogue references an unknown role: " + line.RoleId);
            string panel = dialoguePanelKey;
            string prefix = "/" + panel;
            if (panel == "OptionTalkDialog") hud.ui.SetOptionChoicesVisible(false);
            hud.ui.SetPanelActive(panel, true);
            OfficialMap10DialoguePortraitView portraits;
            if (panel == "TalkDialog") portraits = dialoguePortraits ??= new OfficialMap10DialoguePortraitView(hud.ui, panel, mapClassId);
            else portraits = optionDialoguePortraits ??= new OfficialMap10DialoguePortraitView(hud.ui, panel, mapClassId);
            ShowDialogueContent(panel, portraits, role, line.RoleFaceType,
                dialogueOverrides != null && dialogueOverrides.TryGetValue(line.Order, out string formatted) ? formatted : line.Text,
                !dialogueVoiceSuppressed && line.DialogType == "Voice" ? line.VoiceEventKey : null);
            GameObject next = hud.ui.Find(panel, prefix + "/NextBtn");
            Button button = next == null ? null : next.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveListener(RequestDialogueAdvance);
                button.onClick.AddListener(RequestDialogueAdvance);
            }
        }

        private void RequestDialogueAdvance()
        {
            if (nativeDialogPlayableWaiting || dialogueContentSwitchWaiting) return;
            if (dialogueTypeWriter != null && dialogueTypeWriter.IsPlaying) { dialogueTypeWriter.StopType(); return; }
            dialogueAdvanceRequested = true;
        }

        private void ShowDialogueContent(string panel, OfficialMap10DialoguePortraitView portraits,
            Map10RuntimeDialogRole role, string face, string text, string voice)
        {
            dialogueTypeWriter?.StopType();
            dialogueContentSwitchWaiting = true;
            portraits.SetSpeaker(role, face, () =>
            {
                dialogueContentSwitchWaiting = false;
                string path = "/" + panel + "/Content/CharacterAndTalkContent/TalkContent/";
                hud.ui.SetText(panel, path + "TalkText", text);
                hud.ui.SetText(panel, path + "NpcName/NpcNameText", role.RoleName);
                mapVoice?.RefreshVoice(voice);
                if (!MapProfile.HasNativeUi) return;
                var node = hud.ui.Find(panel, path + "TalkText");
                dialogueTypeWriter = node.GetComponent<WorldDialogueTypeWriter>() ?? node.AddComponent<WorldDialogueTypeWriter>();
                var source = miniMap.miniMapUiData.components.Single(value => value.panel == panel
                    && value.type == (panel == "TalkDialog" ? "TalkDialog" : "OptionTalkDialogPanel"));
                dialogueTypeWriter.StartType(source.fields.mWordInterval);
            });
        }

        private void AdvanceDialogue()
        {
            dialogueAdvanceRequested = false;
            dialogueLineIndex++;
            ShowDialogueLine();
        }
    }
}
