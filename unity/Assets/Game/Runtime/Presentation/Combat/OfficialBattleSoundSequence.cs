using System.Collections.Generic;
using Zjcs.Official.StoryPlayables;

namespace Zjcs.Presentation.Combat
{
    // Extracted prefabs serialize an initialized list. Runtime-created sequences
    // need that list before Unity invokes the inherited editor OnValidate.
    // Scheduling, stopping and all other behaviour remain the original code.
    internal sealed class OfficialBattleSoundSequence : MyPlayableSequence
    {
        public OfficialBattleSoundSequence()
        { myPlayables = new List<MyPlayableTimeCfg>(); }
    }
}
