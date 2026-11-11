// Original main-panel implementation; namespace/base binding only.
using System.Threading;
using Cysharp.Threading.Tasks;
using UIBase = Zjcs.Official.ProfessionUI.UIBase;
namespace Zjcs.Official.HomeMainPanelUI
{

public abstract class MainUIPanelBase : UIBase
{
	public abstract ESceneUIGraphicType SceneUIGraphicType { get; }

	public abstract UniTask Show(CancellationToken cancellationToken);

	public abstract UniTask Hide(CancellationToken cancellationToken);
}

}
