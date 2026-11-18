using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Official.StorySounds;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleGraphCapabilityValidation
    {
        private bool ValidateJumpSlashSound => firstPromotionItemId == 10015
            && Environment.GetEnvironmentVariable("ZJCS_JUMP_SLASH_SOUND") == "1";
        // 10670's cast starts _2 at zero; its 10672 move playable starts _1 at zero.
        private const uint JumpSlashCastSound = 1557672117u;
        private const uint JumpSlashMoveSound = 1557672118u;
        private readonly Dictionary<uint, uint> jumpSlashPlaying = new Dictionary<uint, uint>();
        private bool jumpSlashSoundsComplete;

        private void ObserveJumpSlashSound(OfficialBattleRuntimeController controller)
        {
            if (nativeCasts == 0 || jumpSlashSoundsComplete) return;
            var body = controller.TryGetUnitTransform(1);
            foreach (var sound in UnityEngine.Object.FindObjectsOfType<MyPlayableSound>())
            {
                if (sound.SoundEvent == null || sound.SoundEvent.PlayingId == 0) continue;
                uint eventId = sound.SoundEvent.Id, playing = sound.SoundEvent.PlayingId;
                if ((eventId != JumpSlashCastSound && eventId != JumpSlashMoveSound)
                    || ArcNativeSoundEvent(playing) != eventId || jumpSlashPlaying.ContainsKey(eventId)) continue;
                Require(body && (sound.mySource == body.gameObject || sound.myTarget == body.gameObject),
                    "The original jump-slash sound lost its casting character.");
                float expected = eventId == JumpSlashCastSound ? 0f : .1f;
                Require(Math.Abs(Time.time - firstPromotionCastClock - expected) < .15f,
                    "The original jump-slash cast or movement sound started at the wrong time.");
                jumpSlashPlaying.Add(eventId, playing);
                Debug.Log("[Jump slash sound] Original event " + eventId + " is live in the native engine.");
            }
            if (jumpSlashPlaying.Count == 2 && jumpSlashPlaying.Values.All(id => ArcNativeSoundEvent(id) == 0))
            {
                jumpSlashSoundsComplete = true;
                Debug.Log("[Jump slash sound] Original cast and movement sounds completed.");
            }
        }

        private bool HoldJumpSlashSound()
        {
            if (!captured || jumpSlashSoundsComplete) return false;
            Require(Time.time - firstPromotionCastClock < 12f, "Original jump-slash sounds did not complete.");
            return true;
        }

        private void VerifyJumpSlashSound()
        {
            Require(jumpSlashSoundsComplete && jumpSlashPlaying.Count == 2,
                "Original jump-slash cast and movement sounds were not both verified.");
        }
    }
}
