// Migrated original application/read-query code; see Map13 visibility-view receipt.
using System;




namespace Zjcs.Official.World.Map13EntityVisibilityViews
{
[Flags]
public enum ClientVisibilityType
{
	None = 0,
	Hide = 1,
	WaitRefreshAction = 2,
	WaitAwardItemInited = 4,
	NPCVisibility = 8,
	ConditionVisibility = 0x10
}

}
