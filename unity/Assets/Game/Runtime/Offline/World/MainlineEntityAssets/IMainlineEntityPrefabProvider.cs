using UnityEngine;

namespace Zjcs.Offline.World
{
    public interface IMainlineEntityPrefabProvider
    {
        int MapClassId { get; }
        GameObject LoadClass(int classId);
    }
}
