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
        private bool ValidateWarriorSlashSound => firstPromotionItemId == 10008
            && Environment.GetEnvironmentVariable("ZJCS_WARRIOR_SLASH_SOUND") == "1";
        private bool ValidateLionComboSound => firstPromotionItemId == 10011
            && Environment.GetEnvironmentVariable("ZJCS_LION_COMBO_SOUND") == "1";
        private uint fireSlashPlayingId;
        private bool fireSlashSoundComplete;

        private void ObserveFireSlashSound(OfficialBattleRuntimeController controller)
        {
            if ((firstPromotionItemId != 10026 && !ValidateWarriorSlashSound && !ValidateLionComboSound) || nativeCasts == 0 || fireSlashSoundComplete) return;
            uint expectedEvent = ValidateWarriorSlashSound ? WarriorSlashSoundEvent
                : ValidateLionComboSound ? LionComboSoundEvent : FireSlashSoundEvent;
            float expectedTime = ValidateWarriorSlashSound ? .245f : 0f;
            string soundName = ValidateWarriorSlashSound ? "Skill_10008" : ValidateLionComboSound ? "Skill_10011" : "Skill_10026";
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
                Debug.Log(ValidateLionComboSound
                    ? "[Lion combo sound] Original cast binding, native playback and completion passed."
                    : ValidateWarriorSlashSound
                    ? "[Warrior slash sound] Original cast binding, native playback and completion passed."
                    : "[Fire slash sound] Original cast binding, native playback and completion passed.");
            }
        }

        private bool HoldFireSlashSound()
        {
            if ((firstPromotionItemId != 10026 && !ValidateWarriorSlashSound && !ValidateLionComboSound) || !captured || fireSlashSoundComplete) return false;
            Require(Time.time - firstPromotionCastClock < 12f,
                "Original slash sound did not play and finish through the native engine.");
            return true;
        }

        private void VerifyFireSlashSound()
        {
            if (firstPromotionItemId != 10026 && !ValidateWarriorSlashSound && !ValidateLionComboSound) return;
            Require(fireSlashSoundComplete && fireSlashPlayingId != 0
                && ArcNativeSoundEvent(fireSlashPlayingId) == 0,
                "Original slash native sound verification was incomplete.");
        }
    }
}
