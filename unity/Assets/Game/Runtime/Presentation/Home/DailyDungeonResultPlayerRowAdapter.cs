using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Character;

namespace Zjcs.Presentation.Home
{
    public sealed class DailyDungeonResultPlayerRowAdapter : MonoBehaviour
    {
        public void Configure(HomeFeatureRuntime host)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));

            TMP_Text name = FindNamedComponent<TMP_Text>("PlayerName");
            if (name != null)
                name.text = string.IsNullOrWhiteSpace(host.CharacterName) ? "本地玩家" : host.CharacterName;

            var portrait = OfficialBattleCharacterPortraits.ForProfile(gameObject,
                host.DailyDungeonProgression, host.ActiveProfileSlotId, host.SavesRoot);
            Sprite playerPortrait = portrait(new FightBattleUnitState { Id = 1, CharacterType = 1 });
            if (playerPortrait == null)
                throw new InvalidOperationException("Official daily dungeon player portrait is unavailable.");

            bool bound = false;
            foreach (Image image in GetComponentsInChildren<Image>(true))
            {
                if (!string.Equals(image.gameObject.name, "HeadImg", StringComparison.Ordinal)) continue;
                image.sprite = playerPortrait;
                image.color = Color.white;
                image.preserveAspect = true;
                bound = true;
            }
            foreach (RawImage image in GetComponentsInChildren<RawImage>(true))
            {
                if (!string.Equals(image.gameObject.name, "HeadImg", StringComparison.Ordinal)) continue;
                image.texture = playerPortrait.texture;
                image.color = Color.white;
                bound = true;
            }
            if (!bound)
            {
                throw new InvalidOperationException("Official daily dungeon player portrait slot is missing.");
            }

            Transform thumbsUp = FindNamedTransform("FightThumbsUpUI");
            if (thumbsUp != null) thumbsUp.gameObject.SetActive(false);
            foreach (Button button in GetComponentsInChildren<Button>(true))
                button.interactable = false;
        }

        private T FindNamedComponent<T>(string nodeName) where T : Component
        {
            foreach (T component in GetComponentsInChildren<T>(true))
                if (component != null && string.Equals(component.gameObject.name, nodeName, StringComparison.Ordinal))
                    return component;
            return null;
        }

        private Transform FindNamedTransform(string nodeName)
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
                if (child != null && string.Equals(child.name, nodeName, StringComparison.Ordinal)) return child;
            return null;
        }
    }
}
