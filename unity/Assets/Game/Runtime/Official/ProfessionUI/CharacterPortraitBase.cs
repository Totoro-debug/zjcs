namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using UnityEngine;
using System;

public abstract class CharacterPortraitBase : UIBase
{
	protected CharacterPortraitCache mPortrait;

	private bool mIsTakeUp;

	protected ICharacterPortraitManager CharacterPortraitManager { get; set; }

	public void Bind(ICharacterPortraitManager manager) => CharacterPortraitManager = manager ?? throw new ArgumentNullException(nameof(manager));

	public ICharacterPortraitRequestor Requestor { get; protected set; }

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		BeginRequest(Requestor);
	}

	protected override void OnDisableAndInited()
	{
		ClearAndCancelRequest();
		base.OnDisableAndInited();
	}

	protected void BeginRequest(ICharacterPortraitRequestor requestor)
	{

		if (requestor == null)
		{

			Debug.LogError("BeginRequest 处理的 Requestor为空");
		}
		else if (mIsTakeUp)
		{

			Debug.LogError("玩家肖像连续申请 ");
		}
		else
		{
			mIsTakeUp = true;
			Requestor = requestor;
			mPortrait = CharacterPortraitManager.GetCharacterPortrait(Requestor);
			Requestor.OnTextureUpdate(mPortrait.SubcribeTexture(Requestor));
		}
	}

	protected void ClearAndCancelRequest()
	{

		if (Requestor == null)
		{

			Debug.LogError("ClearAndCancelRequest 时 Requestor为空");
			mPortrait = null;
		}
		else if (!mIsTakeUp)
		{

			Debug.LogError("玩家肖像连续注销 ");
		}
		else
		{
			mIsTakeUp = false;
			mPortrait.Unsubscribe(Requestor);
			Requestor.OnTextureUpdate(null);
			Requestor.StopRequest();
			mPortrait = null;
		}
	}

	protected void SetPortraitData(ICharacterPortraitRequestor request, Action beforeRequestUpdate = null)
	{
		if (CharacterPortraitManager != null)
		{
			beforeRequestUpdate?.Invoke();
			if (IsEnableAndInited())
			{
				ClearAndCancelRequest();
				BeginRequest(request);
			}
			else
			{
				Requestor = request;
				InitWrapper(null);
			}
		}
	}
}

}
