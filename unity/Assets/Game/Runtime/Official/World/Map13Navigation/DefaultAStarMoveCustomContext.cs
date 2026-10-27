namespace Zjcs.Official.World.Map13Navigation
{

public class DefaultAStarMoveCustomContext : IAStarMoveCustomContext
{
	public readonly int MoveMask;

	public DefaultAStarMoveCustomContext(int moveMask)
	{
		MoveMask = moveMask;
	}
}

}
