using System;
using UnityEngine;
using Zjcs.Official.StorySounds;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleGraphCapabilityValidation
    {
        // Original directional fire-slash playables start Skill_10026 at cast time zero.
        private const uint FireSlashSoundEvent = 3761013056u;
        private uint fireSlashPlayingId;
        private bool fireSlashSoundComplete;

        private void ObserveFireSlashSound(OfficialBattleRuntimeController controller)
        {
            if (firstPromotionItemId != 10026 || nativeCasts == 0 || fireSlashSoundComplete) return;
            var body = controller.TryGetUnitTransform(1);
            foreach (var sound in UnityEngine.Object.FindObjectsOfType<MyPlayableSound>())
            {
                if (sound.SoundEvent == null || sound.SoundEvent.Id != FireSlashSoundEvent
                    || sound.SoundEvent.PlayingId == 0) continue;
                uint playing = sound.SoundEvent.PlayingId;
                if (ArcNativeSoundEvent(playing) != FireSlashSoundEvent) continue;
                Require(body && (sound.mySource == body.gameObject || sound.myTarget == body.gameObject),
                    "Original Skill_10026 sound lost its casting character.");
                if (fireSlashPlayingId != 0)
                {
                    Require(fireSlashPlayingId == playing, "Original Skill_10026 sound played more than once.");
                    continue;
                }
                Require(Math.Abs(Time.time - firstPromotionCastClock) < .1f,
                    "Original Skill_10026 sound did not begin at its authored cast time.");
                fireSlashPlayingId = playing;
                Debug.Log("[Fire slash sound] Original Skill_10026 is live in the native engine: " + playing);
            }
            if (fireSlashPlayingId != 0 && ArcNativeSoundEvent(fireSlashPlayingId) == 0)
            {
                fireSlashSoundComplete = true;
                Debug.Log("[Fire slash sound] Original cast binding, native playback and completion passed.");
            }
        }

        private bool HoldFireSlashSound()
        {
            if (firstPromotionItemId != 10026 || !captured || fireSlashSoundComplete) return false;
            Require(Time.time - firstPromotionCastClock < 12f,
                "Original Skill_10026 sound did not play and finish through the native engine.");
            return true;
        }

        private void VerifyFireSlashSound()
        {
            if (firstPromotionItemId != 10026) return;
            Require(fireSlashSoundComplete && fireSlashPlayingId != 0
                && ArcNativeSoundEvent(fireSlashPlayingId) == 0,
                "Original Skill_10026 native sound verification was incomplete.");
        }
    }
}
