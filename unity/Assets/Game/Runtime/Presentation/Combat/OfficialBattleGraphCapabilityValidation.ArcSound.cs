using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Zjcs.Official.StorySounds;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleGraphCapabilityValidation
    {
        private const uint ArcSoundEventId = 3761013058u;
        private bool ValidateArcSound => Environment.GetEnvironmentVariable("ZJCS_ARC_SHIELD_SOUND") == "1";
        private readonly HashSet<uint> arcSoundPlayingIds = new HashSet<uint>();
        private MethodInfo arcSoundEventQuery;

        private uint ArcNativeSoundEvent(uint playingId)
        {
            if (arcSoundEventQuery == null)
            {
                // Keep this presentation assembly independent of the SDK API.
                // Query the same loaded native API used by the formal sound host.
                var api = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType("AkSoundEngine"))
                    .SingleOrDefault(type => type != null);
                Require(api != null, "The original native sound API is unavailable.");
                arcSoundEventQuery = api.GetMethod("GetEventIDFromPlayingID", new[] { typeof(uint) });
                Require(arcSoundEventQuery != null, "Native playing-event query is unavailable.");
            }
            return (uint)arcSoundEventQuery.Invoke(null, new object[] { playingId });
        }

        private void ObserveArcShieldSoundFrame(OfficialBattleRuntimeController controller)
        {
            if (!ValidateArcSound) return;
            Require(ValidateArcShield && firstPromotionItemId == 10024 && skillId == 10760,
                "The original arc sound requires its active-skill lifecycle diagnostic.");
            var body = controller.TryGetUnitTransform(1);
            if (!body || nativeCasts == 0) return;
            foreach (var sound in UnityEngine.Object.FindObjectsOfType<MyPlayableSound>())
            {
                if (sound.SoundEvent == null || sound.SoundEvent.Id != ArcSoundEventId
                    || sound.SoundEvent.PlayingId == 0) continue;
                Require(sound.mySource == body.gameObject || sound.myTarget == body.gameObject,
                    "Original Skill_10024 sound lost its casting character.");
                uint playing = sound.SoundEvent.PlayingId;
                if (ArcNativeSoundEvent(playing) != ArcSoundEventId) continue;
                if (arcSoundPlayingIds.Add(playing))
                    Debug.Log("[Arc shield sound] Original Skill_10024 event=" + ArcSoundEventId
                        + " playing=" + playing + " is live in the native engine.");
            }
        }

        private void VerifyArcShieldSound()
        {
            if (!ValidateArcSound) return;
            Require(arcSoundPlayingIds.Count == 1 && arcSoundPlayingIds.All(id => ArcNativeSoundEvent(id) == 0),
                "Original arc sound did not play once and finish through the native engine.");
            Debug.Log("[Arc shield sound] Original Skill_10024 native playback and completion passed.");
        }
    }
}
