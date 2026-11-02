using System;
using UnityEngine;
using ScenePlayerAppearance = Zjcs.Official.CombatAppearance.PlayerAppearance;

namespace Zjcs.Official.ProfessionUI
{
    public interface ICharacterPortraitAnimation
    {
        void PlayAnimation(string name, bool loop);
        void PrepareForCharacterPortrait();
    }

    // Original HeadCharacterPortraitBuilder. The animation component is the
    // independent native renderer; protocol cloning is an explicit dependency.
    public sealed class HeadCharacterPortraitBuilder : ICharacterPortraitBuilder
    {
        private readonly PlayerPortraitSettings settings;
        private readonly Func<AppearanceData, AppearanceData> clone;
        public HeadCharacterPortraitBuilder(PlayerPortraitSettings settings, Func<AppearanceData, AppearanceData> clone)
        {
            this.settings = settings ? settings : throw new ArgumentNullException(nameof(settings));
            this.clone = clone ?? throw new ArgumentNullException(nameof(clone));
        }

        public void HandleAppearanceCharacter(Component characterAppearance, ICharacterPortraitRequestor request)
        {
            var animation = characterAppearance.GetComponent<ICharacterPortraitAnimation>();
            if (animation == null)
            {
                Debug.LogError("characterAppearance 没有获得 SkeletonAnimation 组件");
            }
            else if (!characterAppearance.TryGetComponent<ScenePlayerAppearance>(out var playerAppearance))
            {
                Debug.LogError("characterAppearance 没有获得 PlayerAppearance 组件");
            }
            else
            {
                AppearanceData portraitAppearanceData = GetPortraitAppearanceData(request);
                playerAppearance.RefreshAppearance(0L, (Sex)request.GetSex(), request.GetProfession(), portraitAppearanceData);
                animation.PlayAnimation(request.GetAnimationName(), loop: true);
                animation.PrepareForCharacterPortrait();
            }
        }

        // Original CharacterPortraitUtils.GetPortraitAppearanceData. Keep the
        // original object when no exclusion exists; never edit the saved player.
        private AppearanceData GetPortraitAppearanceData(ICharacterPortraitRequestor requestor)
        {
            AppearanceData appearanceData = (AppearanceData)requestor.GetAppearanceData();
            PlayerPortraitSettings.PhotographyParameters param = requestor.GetParam(settings);
            if (param == null || !param.HasExcludeConfig) return appearanceData;
            AppearanceData appearanceData2 = clone(appearanceData);
            foreach (SkinType excludeSkinType in param.ExcludeSkinTypes)
            {
                appearanceData2.SkinMap.Remove(excludeSkinType);
                appearanceData2.AppendAppearanceDict.Remove(excludeSkinType);
            }
            return appearanceData2;
        }
    }
}
