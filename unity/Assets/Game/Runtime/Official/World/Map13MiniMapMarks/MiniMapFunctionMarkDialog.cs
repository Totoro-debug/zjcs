using Cysharp.Threading.Tasks;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.StoryPlayables;
using IWindowOperator=Zjcs.Official.ProfessionUI.IWindowOperator;
public abstract class MiniMapFunctionMarkDialog : MiniMapMarkDialogBase
{
	public Button Btn;

	private IMiniMapCharacterActionGroupHandle MapCharacterActionGroupHandle => miniMapMark.MiniMap.CharacterActionGroup;

	protected override void Awake()
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Expected Obj, but got Unknown
		base.Awake();
		((UnityEvent)Btn.onClick).AddListener((UnityAction)OnClick);
	}

	protected override void OnDestroy()
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Expected Obj, but got Unknown
		((UnityEvent)Btn.onClick).RemoveListener((UnityAction)OnClick);
		base.OnDestroy();
	}

	public override void Init(MiniMapMarkUIBase mark)
	{
		base.Init(mark);
	}

	protected virtual void OnClick()
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		if (MapCharacterActionGroupHandle != null)
		{
			UniTaskExtensions.Forget(MapCharacterActionGroupHandle.ConfirmInterruptAndContinueAsync(DoClick));
		}
		else
		{
			DoClick();
		}
	}

	private void DoClick()
	{
		miniMapMark.Mark.OnClick();
		Close();
		miniMapMark.MiniMap.FireToCloseFullScreen();
	}
}

}
