using UnityEngine;

namespace Zjcs.Offline.World
{
    public interface IMainlineEntityPrefabProvider
    {
        int MapClassId { get; }
        string SourceManifest { get; }
        GameObject LoadClass(int classId);
    }
}
