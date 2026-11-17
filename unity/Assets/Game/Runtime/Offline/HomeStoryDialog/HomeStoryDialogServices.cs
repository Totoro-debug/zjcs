using System;
using System.Collections.Generic;
using Common;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.HomeStoryDialog;
using Zjcs.Official.HomeStoryUI;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.HomeStory;
using Zjcs.Offline.HomeUI;

namespace Zjcs.Offline.HomeStoryDialog
{
    // Scene-dependent services are supplied by the home runtime that owns the window.
    public interface IHomeStoryDialogEnvironment
    {
        string PlayerName { get; }
        int PlayerRoleId { get; }
        RegionCodeEnum Region();
        DateTime Now();
        bool IsConditionMet(int conditionType, string conditionParams);
        bool ContainsText(string key);
        string Localize(string key);
        WorldPopupWindow LastWindowIgnoringTips();
        bool ActivePanelHasSceneGraphic();
        void CloseWindow(GameObject window);
        void PostVoice(string eventKey, AudioClip clip, GameObject emitter);
        MyPlayable PlayWorldNpcPlayable(int roleId, string path);
        void ReleasePlayableResources();
    }

    public sealed class HomeStoryDialogServices : IStoryDialogServices
    {
        private readonly IHomeStoryDialogEnvironment environment;
        private readonly HomeStorySettings settings;
        private readonly HomeStoryCharacterServices characters;
        private readonly HomeStoryOptionAssets optionAssets;
        private readonly TMP_FontAsset font;
        private readonly Dictionary<string, List<StoryDialogInfo>> dialogues = new Dictionary<string, List<StoryDialogInfo>>();
        private readonly Dictionary<int, StoryDialogRoleInfo> roles = new Dictionary<int, StoryDialogRoleInfo>();
        private readonly Dictionary<string, StoryOptionInfo> options = new Dictionary<string, StoryOptionInfo>();
        private readonly Dictionary<string, string> texts = new Dictionary<string, string>();
        private readonly Dictionary<string, AudioClip> voices = new Dictionary<string, AudioClip>();

        public HomeStoryDialogServices(HomeStoryDialogAssets assets, HomeStoryPortraitAssets portraits,
            HomeStoryOptionAssets optionAssets, HomeStorySettings settings, TMP_FontAsset font,
            IHomeStoryDialogEnvironment environment)
        {
            if (!assets || !assets.StorySource || assets.Voices == null || !portraits || !optionAssets || !font)
                throw new ArgumentException("The original story dialogue assets are incomplete.");
            this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.optionAssets = optionAssets;
            this.font = font;
            characters = new HomeStoryCharacterServices(portraits, environment.Region, settings.IsVoiceOver);
            JObject root = JObject.Parse(assets.StorySource.text);
            var groups = root["dialogues"] as JObject ?? throw new InvalidOperationException("Original story dialogues are missing.");
            foreach (var story in groups.Properties())
            {
                if (!(story.Value is JObject byId)) throw new InvalidOperationException("Invalid original story dialogue groups.");
                foreach (var group in byId.Properties())
                {
                    if (!int.TryParse(group.Name, out int id)) throw new InvalidOperationException("Invalid original dialogue ID.");
                    dialogues.Add(Key(story.Name, id), group.Value.ToObject<List<StoryDialogInfo>>());
                }
            }
            foreach (var role in root["dialogRoles"].ToObject<List<StoryDialogRoleInfo>>()) roles.Add(role.RoleId, role);
            foreach (var option in root["storyOptions"].ToObject<List<StoryOptionInfo>>()) options.Add(Key(option.StoryName, option.Id), option);
            foreach (var text in ((JObject)root["localization"]).Properties()) texts.Add(text.Name, (string)text.Value);
            foreach (var row in assets.Voices)
            {
                if (row == null || string.IsNullOrEmpty(row.EventKey) || !row.Clip)
                    throw new InvalidOperationException("An original story voice is not bound.");
                voices.Add(row.EventKey, row.Clip);
            }
        }

        private static string Key(string storyName, int id) => storyName + "\0" + id;
        public string PlayerName => environment.PlayerName;
        public int PlayerRoleId => environment.PlayerRoleId;
        public bool HasStorySettings => true;
        public string DialogVoiceStopEventKey => settings.DialogVoiceStopEventKey;
        public string TalkBubblePlayablePath => settings.TalkBubblePlayablePath;
        public RegionCodeEnum Region() => environment.Region();
        public DateTime Now() => environment.Now();
        public bool IsVoiceOver(int roleId) => settings.IsVoiceOver(roleId);
        public bool IsConditionMet(int type, string parameters) => environment.IsConditionMet(type, parameters);
        public TMP_FontAsset Font() => font;
        public IHomeHudAssetHandle<GameObject> LoadPortrait(string path) => characters.LoadPortrait(path);
        public StoryDialogRoleInfo GetDialogRole(int roleId) => roles.TryGetValue(roleId, out var role) ? role : null;
        public StoryOptionInfo GetStoryOptionInfo(string name, int id) => options.TryGetValue(Key(name, id), out var option) ? option : null;
        public List<StoryDialogInfo> GetDialogInfos(string name, int id)
            => dialogues.TryGetValue(Key(name, id), out var result) ? result : null;
        public bool ContainsText(string key) => texts.ContainsKey(key) || environment.ContainsText(key);
        public string Localize(string key) => texts.TryGetValue(key, out string value) ? value : environment.Localize(key);
        public IStoryOptionSpriteLease LoadSprite(string path)
        {
            if (path != "UI/Dialogue/Iconarrow_1" || !optionAssets.IconArrow1)
                throw new InvalidOperationException("Unbound original story option Sprite: " + path);
            return new SpriteLease(optionAssets.IconArrow1);
        }
        public WorldPopupWindow LastWindowIgnoringTips() => environment.LastWindowIgnoringTips();
        public bool ActivePanelHasSceneGraphic() => environment.ActivePanelHasSceneGraphic();
        public void CloseWindow(GameObject window) => environment.CloseWindow(window);
        public MyPlayable PlayWorldNpcPlayable(int roleId, string path) => environment.PlayWorldNpcPlayable(roleId, path);
        public void ReleasePlayableResources() => environment.ReleasePlayableResources();
        public void PostVoice(string eventKey, GameObject emitter)
        {
            if (eventKey == settings.DialogVoiceStopEventKey) { environment.PostVoice(eventKey, null, emitter); return; }
            if (!voices.TryGetValue(eventKey, out var clip)) throw new InvalidOperationException("Unbound original story voice: " + eventKey);
            environment.PostVoice(eventKey, clip, emitter);
        }

        private sealed class SpriteLease : IStoryOptionSpriteLease
        {
            public Sprite Result { get; }
            public SpriteLease(Sprite sprite) { Result = sprite; }
            public void Dispose() { }
        }
    }
}
