using System;
using UnityEngine;
using Zjcs.Official.StorySounds;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleGraphCapabilityValidation
    {
        // Original playable10680 starts Skill_10016 at cast time zero.
        private const uint LightningDashSoundEvent = 3744235477u;
        private uint lightningDashPlayingId;
        private bool lightningDashSoundComplete;

        private void ObserveLightningDashSound(OfficialBattleRuntimeController controller)
        {
            if (firstPromotionItemId != 10016 || nativeCasts == 0 || lightningDashSoundComplete) return;
            var body = controller.TryGetUnitTransform(1);
            foreach (var sound in UnityEngine.Object.FindObjectsOfType<MyPlayableSound>())
            {
                if (sound.SoundEvent == null || sound.SoundEvent.Id != LightningDashSoundEvent
                    || sound.SoundEvent.PlayingId == 0) continue;
                uint playing = sound.SoundEvent.PlayingId;
                if (ArcNativeSoundEvent(playing) != LightningDashSoundEvent) continue;
                Require(body && (sound.mySource == body.gameObject || sound.myTarget == body.gameObject),
                    "Original Skill_10016 sound lost its casting character.");
                if (lightningDashPlayingId != 0)
                {
                    Require(lightningDashPlayingId == playing, "Original Skill_10016 sound played more than once.");
                    continue;
                }
                Require(Math.Abs(Time.time - firstPromotionCastClock) < .1f,
                    "Original Skill_10016 sound did not begin at its authored cast time.");
                lightningDashPlayingId = playing;
                Debug.Log("[Lightning dash sound] Original Skill_10016 is live in the native engine: " + playing);
            }
            if (lightningDashPlayingId != 0 && ArcNativeSoundEvent(lightningDashPlayingId) == 0)
            {
                lightningDashSoundComplete = true;
                Debug.Log("[Lightning dash sound] Original cast binding, native playback and completion passed.");
            }
        }

        private bool HoldLightningDashSound()
        {
            if (firstPromotionItemId != 10016 || !captured || lightningDashSoundComplete) return false;
            Require(Time.time - firstPromotionCastClock < 12f,
                "Original Skill_10016 sound did not play and finish through the native engine.");
            return true;
        }

        private void VerifyLightningDashSound()
        {
            if (firstPromotionItemId != 10016) return;
            Require(lightningDashSoundComplete && lightningDashPlayingId != 0
                && ArcNativeSoundEvent(lightningDashPlayingId) == 0,
                "Original Skill_10016 native sound verification was incomplete.");
        }
    }
}
