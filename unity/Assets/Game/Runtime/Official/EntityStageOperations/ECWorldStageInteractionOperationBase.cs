// Migrated complete original stage operation; see stage operation receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityWorldStages;
using StageDialog = Zjcs.Official.WorldUI.StageDialog;
using System;
using System.Collections.Generic;

namespace Zjcs.Official.EntityStageOperations
{
public abstract class ECWorldStageInteractionOperationBase : IECWorldStageInteractionOperation
{
	public abstract bool DoOper(IServiceProvider serviceProvider, ECWorldStageStateData worldStageComponent);

	protected void StartStage(ECWorldStageStateData worldStageComponent, int subIndex, Dictionary<StageComponentType, object> conComponentParamDict = null, object stageData = null, Action<object, int> callback = null)
	{
		if (worldStageComponent != null && WorldStageOperationServices.State(worldStageComponent).IsActive())
		{
			((IWorldStageInteractionStarter)WorldStageOperationServices.ApplicationScope.GetService(typeof(IWorldStageInteractionStarter))).StartStage(worldStageComponent, subIndex, conComponentParamDict, stageData, callback);
		}
	}

	public abstract bool ExistOperation<T>() where T : IECWorldStageInteractionOperation;

	protected bool OperationIsThis<T>() where T : IECWorldStageInteractionOperation
	{
		return this is T;
	}

	public virtual void Release()
	{
	}
}

}
