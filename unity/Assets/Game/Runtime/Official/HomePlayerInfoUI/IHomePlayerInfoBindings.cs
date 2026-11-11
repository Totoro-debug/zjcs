using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.HomePlayerInfoUI
{
    public interface IHomePlayerInfoBindings : IHomeExperienceServices
    {
        new IHomePlayerInfoPlayer MainPlayer { get; }
        bool TMPEffectFontSizeIssueFixed { get; }
        Vector3 PointerPosition { get; }
        string Text(string key);
        void PopupIntroduction(UIBase owner, HomeInfoTextKey title, IReadOnlyList<string> content, Vector3 position);
    }

    public interface IHomePlayerInfoPlayer : IHomeExperiencePlayer
    {
        string Name { get; }
        long CombatRating { get; }
        event Action OnNameUpdate;
        event Action<int> OnLevelUpdate;
        event Action<long, long> OnCombatRatingChanged;
    }

    // The original TextKeyArgWrap is passed unchanged across the window boundary.
    public sealed class HomeInfoTextKey
    {
        public string Key { get; }
        public HomeInfoTextKey(string key) { Key = key; }
    }
}
