using System;

namespace Zjcs.Official.ProfessionUI
{
    // Application boundary for the original weapon rotation overrides. Applied
    // rotation is the local input at UpdateLocal, before world constraints.
    public interface IProfessionLocalOverrideBone
    {
        float Rotation { get; set; }
        float AppliedRotation { get; set; }
    }

    public interface IProfessionBoneOverrideAnimation
    {
        IProfessionLocalOverrideBone FindOverrideBone(string name);
        event Action<IProfessionBoneOverrideAnimation> UpdateLocal;
        event Action<IProfessionBoneOverrideAnimation> UpdateComplete;
    }

    public enum OverrideUpdateMode { Local, Complete }
    public enum OverrideRotMode { Offset, Override }
}
