using System.Linq;
using UnityEngine;
using Zjcs.Official.BattleBgm;

namespace Zjcs.Validation.Combat
{
    public static partial class OfficialBattlePublicFlowValidation
    {
        private static bool HasNativeSound(GameObject emitter, uint eventId = 0)
        {
            ulong id = AkSoundEngine.GetAkGameObjectID(emitter);
            uint count = 0;
            Require(AkSoundEngine.GetPlayingIDsFromGameObject(id, ref count, null) == AKRESULT.AK_Success,
                "Query native playing count.");
            if (count == 0) return false;
            var playing = new uint[count];
            Require(AkSoundEngine.GetPlayingIDsFromGameObject(id, ref count, playing) == AKRESULT.AK_Success,
                "Query native playing IDs.");
            return playing.Take((int)count).Any(value => eventId == 0
                || AkSoundEngine.GetEventIDFromPlayingID(value) == eventId);
        }

        private static void CheckNativeMusic()
        {
            Require(AkSoundEngine.IsInitialized(), "The formal sound engine is live.");
            var provider = UnityEngine.Object.FindObjectsOfType<BgmGameObject>().Single();
            Require(AkSoundEngine.GetSwitch("BattleMusic", AkSoundEngine.GetAkGameObjectID(provider.gameObject),
                out uint selection) == AKRESULT.AK_Success && selection == 753380097u,
                "The original scene music switch is Map_11_1_Map.");
            Require(HasNativeSound(provider.gameObject, 3912018356u), "The original BattleBgm_Play is playing.");
        }
    }
}
