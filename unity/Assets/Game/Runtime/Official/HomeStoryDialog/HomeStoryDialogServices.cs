using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using Zjcs.Official.HomeStoryCharacter;
using Zjcs.Official.HomeStoryUI;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.HomeStoryDialog
{
    [Serializable] public sealed class StoryDialogInfo
    {
        public string StoryName, VoiceEventKey, RoleFaceType, PlayablePath;
        public int Id, Order, DialogType, RoleId;
    }
    [Serializable] public sealed class StoryDialogRoleInfo
    {
        public int RoleId, EntityClassId;
        public string PortraitPath;
    }

    // Ports correspond to original parsers, language, window, audio and scene services.
    public interface IStoryDialogServices : IStoryPortraitServices, IStoryOptionServices
    {
        string PlayerName { get; }
        int PlayerRoleId { get; }
        bool HasStorySettings { get; }
        string DialogVoiceStopEventKey { get; }
        string TalkBubblePlayablePath { get; }
        List<StoryDialogInfo> GetDialogInfos(string storyName, int dialogId);
        StoryDialogRoleInfo GetDialogRole(int roleId);
        WorldPopupWindow LastWindowIgnoringTips();
        bool ActivePanelHasSceneGraphic();
        void CloseWindow(GameObject window);
        void PostVoice(string eventKey, GameObject emitter);
        MyPlayable PlayWorldNpcPlayable(int roleId, string path);
        void ReleasePlayableResources();
    }

    public static class StoryDialogLanguage
    {
        public static string DialogTextKey(string storyName, int id, int order) => $"dialog_{storyName}_{id}_{order}";
        public static StoryTextKeyArgWrap RoleName(int roleId, int playerRoleId, string playerName)
            => roleId == playerRoleId ? new StoryTextKeyArgWrap("{0}", playerName)
                : new StoryTextKeyArgWrap($"dialog_role_{roleId}_name");
        public static string ReplacePlayerName(string text, string playerName)
            => Regex.IsMatch(text, "<PN>") ? text.Replace("<PN>", playerName) : text;
        public static string VoiceEventKey(StoryDialogInfo info)
            => info == null || info.DialogType != 1 ? string.Empty : info.VoiceEventKey;
    }
}
