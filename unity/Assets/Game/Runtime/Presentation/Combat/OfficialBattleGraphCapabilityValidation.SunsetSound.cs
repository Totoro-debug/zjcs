using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Official.StorySounds;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleGraphCapabilityValidation
    {
        private bool ValidateSunsetSound => Environment.GetEnvironmentVariable("ZJCS_SUNSET_SWORD_SOUND") == "1";
        // Original playable11250: Skill_10053_1, _2, _3 at 0, .5, 1.25 seconds.
        private static readonly uint[] SunsetSoundEvents = { 3496259616u, 3496259619u, 3496259618u };
        private static readonly float[] SunsetSoundTimes = { 0f, .5f, 1.25f };
        private readonly Dictionary<uint, uint> sunsetSoundPlayingIds = new Dictionary<uint, uint>();
        private readonly List<uint> sunsetSoundOrder = new List<uint>();
        private bool sunsetSoundsComplete;

        private void ObserveSunsetSwordSoundFrame(OfficialBattleRuntimeController controller)
        {
            if (!ValidateSunsetSound || nativeCasts == 0 || sunsetSoundsComplete) return;
            Require(firstPromotionItemId == 10053 && skillId == 11250,
                "The sunset sound diagnostic requires original 10053/11250.");
            var body = controller.TryGetUnitTransform(1);
            foreach (var sound in UnityEngine.Object.FindObjectsOfType<MyPlayableSound>())
            {
                if (sound.SoundEvent == null || !SunsetSoundEvents.Contains(sound.SoundEvent.Id)
                    || sound.SoundEvent.PlayingId == 0) continue;
                uint eventId = sound.SoundEvent.Id, playing = sound.SoundEvent.PlayingId;
                if (ArcNativeSoundEvent(playing) != eventId) continue;
                Require(body && (sound.mySource == body.gameObject || sound.myTarget == body.gameObject),
                    "An original Skill_10053 sound lost its casting character.");
                if (sunsetSoundPlayingIds.TryGetValue(eventId, out uint previous))
                {
                    Require(previous == playing, "An original Skill_10053 sound played more than once.");
                    continue;
                }
                int index = Array.IndexOf(SunsetSoundEvents, eventId);
                float elapsed = Time.time - firstPromotionCastClock;
                Require(index == sunsetSoundOrder.Count && Math.Abs(elapsed - SunsetSoundTimes[index]) < .1f,
                    "Original Skill_10053 native sound order or authored time differs: event=" + eventId + "; time=" + elapsed);
                sunsetSoundPlayingIds.Add(eventId, playing); sunsetSoundOrder.Add(eventId);
                Debug.Log("[Sunset sword sound] Original event=" + eventId + " playing=" + playing
                    + " is live in the native engine at cast time=" + elapsed);
            }
            if (sunsetSoundPlayingIds.Count == 3
                && sunsetSoundPlayingIds.Values.All(id => ArcNativeSoundEvent(id) == 0))
            {
                sunsetSoundsComplete = true;
                Debug.Log("[Sunset sword sound] Original three native events, authored order/times and completion passed.");
            }
        }

        private bool HoldSunsetSwordSound()
        {
            if (!ValidateSunsetSound || !captured || sunsetSoundsComplete) return false;
            Require(Time.time - firstPromotionCastClock < 12f,
                "Original Skill_10053 sounds did not all play and finish through the native engine.");
            return true;
        }

        private void VerifySunsetSwordSound()
        {
            if (!ValidateSunsetSound) return;
            Require(sunsetSoundsComplete && sunsetSoundOrder.SequenceEqual(SunsetSoundEvents)
                && sunsetSoundPlayingIds.Values.All(id => ArcNativeSoundEvent(id) == 0),
                "Original Skill_10053 native sound verification was incomplete.");
        }
    }
}
