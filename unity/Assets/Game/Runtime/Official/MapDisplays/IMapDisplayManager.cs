// Complete original map-display application class; see source receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.World.Map13Entities;
using Zjcs.Official.StoryPlayables;
using UnityEngine;

namespace Zjcs.Official.MapDisplays
{
public interface IMapDisplayManager
{
	IECViewDriver Acquire(int classId, int mapClassId, string areaTag, GroundType groundType, string tag, Transform content);

	void Recycle(int classId, int mapClassId, string areaTag, GroundType groundType, string tag, IECViewDriver driver);
}

}
