using UnityEngine;

namespace Zjcs.Official.BattleBgm
{
    // The two Wwise operations consumed by the original manager. Native playback
    // and the recording acceptance adapter use the same interface.
    public interface IBgmPlayback
    {
        uint PostEvent(string eventName, GameObject emitter);
        void SetSwitch(string group, string value, GameObject emitter);
    }
}
