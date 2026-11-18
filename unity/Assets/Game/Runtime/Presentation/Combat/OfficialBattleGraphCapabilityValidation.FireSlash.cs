using System;
using UnityEngine;
using Zjcs.Official.StorySounds;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleGraphCapabilityValidation
    {
        // Original directional fire-slash playables start Skill_10026 at cast time zero.
        private const uint FireSlashSoundEvent = 3761013056u;
        // Original playable10600 starts Skill_10008 at .245 seconds.
        private const uint WarriorSlashSoundEvent = 3727457896u;
        // Original playable10630 starts Skill_10011 at cast time zero.
        private const uint LionComboSoundEvent = 3744235474u;
        // Original playable10640 starts Skill_10012 at .15 seconds.
        private const uint WindBladeSoundEvent = 3744235473u;
        // Original playable10650 starts Skill_10013 at cast time zero.
        private const uint FourWaySlashSoundEvent = 3744235472u;
        // Original playable10660 starts Skill_10014_1 at .4 seconds.
        private const uint WhirlwindSlashSoundEvent = 1557819183u;
        // Original playable10700 starts Skill_10018 at cast time zero.
        private const uint PullSoundEvent = 3744235483u;
        // Original playable10590 starts Skill_10043_1 at .2 seconds.
        private const uint FallingWindSoundEvent = 2749055671u;
        private bool ValidateWarriorSlashSound => firstPromotionItemId == 10008
            && Environment.GetEnvironmentVariable("ZJCS_WARRIOR_SLASH_SOUND") == "1";
        private bool ValidateLionComboSound => firstPromotionItemId == 10011
            && Environment.GetEnvironmentVariable("ZJCS_LION_COMBO_SOUND") == "1";
        private bool ValidateWindBladeSound => firstPromotionItemId == 10012
            && Environment.GetEnvironmentVariable("ZJCS_WIND_BLADE_SOUND") == "1";
        private bool ValidateFourWaySlashSound => firstPromotionItemId == 10013
            && Environment.GetEnvironmentVariable("ZJCS_FOUR_WAY_SLASH_SOUND") == "1";
        private bool ValidateWhirlwindSlashSound => firstPromotionItemId == 10014
            && Environment.GetEnvironmentVariable("ZJCS_WHIRLWIND_SLASH_SOUND") == "1";
        private bool ValidatePullSound => firstPromotionItemId == 10018
            && Environment.GetEnvironmentVariable("ZJCS_PULL_SOUND") == "1";
        private bool ValidateFallingWindSound => firstPromotionItemId == 10043
            && Environment.GetEnvironmentVariable("ZJCS_FALLING_WIND_SOUND") == "1";
        private uint fireSlashPlayingId;
        private bool fireSlashSoundComplete;

        private void ObserveFireSlashSound(OfficialBattleRuntimeController controller)
        {
            if (ValidateJumpSlashSound) { ObserveJumpSlashSound(controller); return; }
            if ((firstPromotionItemId != 10026 && !ValidateWarriorSlashSound && !ValidateLionComboSound && !ValidateWindBladeSound && !ValidateFourWaySlashSound && !ValidateWhirlwindSlashSound && !ValidatePullSound && !ValidateFallingWindSound) || nativeCasts == 0 || fireSlashSoundComplete) return;
            uint expectedEvent = ValidateWarriorSlashSound ? WarriorSlashSoundEvent
                : ValidateLionComboSound ? LionComboSoundEvent : ValidateWindBladeSound ? WindBladeSoundEvent
                : ValidateFourWaySlashSound ? FourWaySlashSoundEvent : ValidateWhirlwindSlashSound ? WhirlwindSlashSoundEvent : ValidatePullSound ? PullSoundEvent : ValidateFallingWindSound ? FallingWindSoundEvent : FireSlashSoundEvent;
            float expectedTime = ValidateWarriorSlashSound ? .245f : ValidateWindBladeSound ? .15f : ValidateWhirlwindSlashSound ? .4f : ValidateFallingWindSound ? .2f : 0f;
            string soundName = ValidateWarriorSlashSound ? "Skill_10008" : ValidateLionComboSound ? "Skill_10011"
                : ValidateWindBladeSound ? "Skill_10012" : ValidateFourWaySlashSound ? "Skill_10013" : ValidateWhirlwindSlashSound ? "Skill_10014_1" : ValidatePullSound ? "Skill_10018" : ValidateFallingWindSound ? "Skill_10043_1" : "Skill_10026";
            var body = controller.TryGetUnitTransform(1);
            foreach (var sound in UnityEngine.Object.FindObjectsOfType<MyPlayableSound>())
            {
                if (sound.SoundEvent == null || sound.SoundEvent.Id != expectedEvent
                    || sound.SoundEvent.PlayingId == 0) continue;
                uint playing = sound.SoundEvent.PlayingId;
                if (ArcNativeSoundEvent(playing) != expectedEvent) continue;
                Require(body && (sound.mySource == body.gameObject || sound.myTarget == body.gameObject),
                    "Original " + soundName + " sound lost its casting character.");
                if (fireSlashPlayingId != 0)
                {
                    Require(fireSlashPlayingId == playing, "Original " + soundName + " sound played more than once.");
                    continue;
                }
                Require(Math.Abs(Time.time - firstPromotionCastClock - expectedTime) < .1f,
                    "Original " + soundName + " sound did not begin at its authored cast time.");
                fireSlashPlayingId = playing;
                Debug.Log("[Fire slash sound] Original " + soundName + " is live in the native engine: " + playing);
            }
            if (fireSlashPlayingId != 0 && ArcNativeSoundEvent(fireSlashPlayingId) == 0)
            {
                fireSlashSoundComplete = true;
                Debug.Log(ValidateFallingWindSound
                    ? "[Falling wind sound] Original cast binding, native playback and completion passed."
                    : ValidatePullSound
                    ? "[Pull sound] Original cast binding, native playback and completion passed."
                    : ValidateWhirlwindSlashSound
                    ? "[Whirlwind slash sound] Original cast binding, native playback and completion passed."
                    : ValidateFourWaySlashSound
                    ? "[Four-way slash sound] Original cast binding, native playback and completion passed."
                    : ValidateWindBladeSound
                    ? "[Wind blade sound] Original cast binding, native playback and completion passed."
                    : ValidateLionComboSound
                    ? "[Lion combo sound] Original cast binding, native playback and completion passed."
                    : ValidateWarriorSlashSound
                    ? "[Warrior slash sound] Original cast binding, native playback and completion passed."
                    : "[Fire slash sound] Original cast binding, native playback and completion passed.");
            }
        }

        private bool HoldFireSlashSound()
        {
            if (ValidateJumpSlashSound) return HoldJumpSlashSound();
            if ((firstPromotionItemId != 10026 && !ValidateWarriorSlashSound && !ValidateLionComboSound && !ValidateWindBladeSound && !ValidateFourWaySlashSound && !ValidateWhirlwindSlashSound && !ValidatePullSound && !ValidateFallingWindSound) || !captured || fireSlashSoundComplete) return false;
            Require(Time.time - firstPromotionCastClock < 12f,
                "Original slash sound did not play and finish through the native engine.");
            return true;
        }

        private void VerifyFireSlashSound()
        {
            if (ValidateJumpSlashSound) { VerifyJumpSlashSound(); return; }
            if (firstPromotionItemId != 10026 && !ValidateWarriorSlashSound && !ValidateLionComboSound && !ValidateWindBladeSound && !ValidateFourWaySlashSound && !ValidateWhirlwindSlashSound && !ValidatePullSound && !ValidateFallingWindSound) return;
            Require(fireSlashSoundComplete && fireSlashPlayingId != 0
                && ArcNativeSoundEvent(fireSlashPlayingId) == 0,
                "Original slash native sound verification was incomplete.");
        }
    }
}
