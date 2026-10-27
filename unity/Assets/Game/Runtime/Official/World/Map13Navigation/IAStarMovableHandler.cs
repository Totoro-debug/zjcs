using Zjcs.Official.World.NativeTerrain.Common;

namespace Zjcs.Official.World.Map13Navigation
{

public interface IAStarMovableHandler
{
	bool IsFit(Pos2d pos, IAStarMoveCustomContext customContext);

	bool CanMoveToNext(Pos2d posFrom, Pos2d posTo);
}

}
