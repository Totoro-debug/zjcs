using System;
using TMPro;
using UnityEngine;

namespace Zjcs.Official.HomeStoryUI
{
    // Service ports correspond to the original option parser, condition, language,
    // and resource-loader calls. The owner supplies the selected local services.
    public interface IStoryOptionServices
    {
        StoryOptionInfo GetStoryOptionInfo(string storyName, int id);
        bool IsConditionMet(int conditionType, string conditionParams);
        bool ContainsText(string key);
        string Localize(string key);
        TMP_FontAsset Font();
        DateTime Now();
        IStoryOptionSpriteLease LoadSprite(string path);
    }

    public interface IStoryOptionSpriteLease : IDisposable { Sprite Result { get; } }

    public sealed class StoryOptionInfo
    {
        public string StoryName, Icon, BlackIcon, ConditionParams;
        public int Id, ConditionType;
    }

    [Serializable]
    public struct StoryTextKeyArgWrap
    {
        public readonly string Key;
        public object[] Args;
        public StoryTextKeyArgWrap(string key, params object[] args) { Key = key; Args = args; }
    }

    public static class StoryOptionLanguage
    {
        // Common.LanguageUtils methods from the selected official client.
        public static string GetStoryOptionTextKey(string storyName, int optionId)
            => $"story_{storyName}_{optionId}_option";
        public static string GetStoryOptionMinirTextKey(string storyName, int optionId)
            => $"story_{storyName}_{optionId}_option_minir";
    }
}
