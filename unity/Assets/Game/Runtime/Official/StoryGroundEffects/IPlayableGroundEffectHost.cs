using System;
using UnityEngine;

namespace Zjcs.Official.StoryGroundEffects
{
    // Host resolves the active fight ground before map ground, matching the
    // original IFightRootProxy/IMapRootManagerProxy selection. A surface keeps
    // the original PosToGrid -> Info.GetGridGroundType conversion.
    public interface IPlayableGroundSurface
    {
        GroundType GetGroundType(Vector3 attachmentLocalPosition);
    }

    public interface IPlayableGroundEffectAsset : IDisposable
    {
        GameObject Result { get; }
    }

    public interface IPlayableGroundEffectHost
    {
        IPlayableGroundSurface ResolveGround();
        IPlayableGroundEffectAsset LoadAssetByGuid(string guid);
        GameObject Instantiate(GameObject prefab);
    }
}
