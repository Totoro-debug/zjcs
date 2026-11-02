// Ported selected official client; see partner-star-god-ui-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using Zjcs.Official.WorldUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.Partners;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class NpcPanelStarGodContent : MonoBehaviour, LoopScrollDataSource
{
    private IPartnerStarGodUiServices services;
    public void Configure(IPartnerStarGodUiServices value)
    {
        if (services != null) throw new System.InvalidOperationException("Star-god list is already configured.");
        services = value ?? throw new System.ArgumentNullException(nameof(value));
        if (isActiveAndEnabled) OnEnable();
    }

	[SerializeField]
	private LoopVerticalScrollRect mLoop;

	[SerializeField]
	private RectTransform mContent;

	[SerializeField]
	private StarGodNpcItemGroupItem mTemplateGroupItem;

	private List<NpcRosterItem> mStarGodNpcs = new List<NpcRosterItem>();

	private void OnEnable()
	{
		if (services == null) return;
		services.NpcCollectedChanged += OnNpcCollectChanged;
		RefreshList();
	}

	private void OnDisable()
	{
		if (services != null && services.MainPlayerAvailable)
		{
			services.NpcCollectedChanged -= OnNpcCollectChanged;
		}
	}

	private void OnNpcCollectChanged(int npcClassId, bool isCollected)
	{
		RefreshList();
	}

	private void InitNpcList()
	{
		mStarGodNpcs.Clear();
		foreach (PartnerStarGodNpcDefinition npc in services.Definitions)
		{
			if (npc.IsMapExploreBoss || !npc.IsStarGodNpc)
			{
				continue;
			}
			NpcMeetStatus npcMeetStatus = services.MeetStatusOf(npc.Id);
			if (npcMeetStatus != NpcMeetStatus.NotMeet)
			{
				NpcRosterItem playerNpcDataWrap = services.GetNpc(npc.Id);
				if (playerNpcDataWrap == null)
				{
					playerNpcDataWrap = services.CreatePreviewNpc(npc, 1);
				}
				playerNpcDataWrap.MeetStatus = npcMeetStatus;
				mStarGodNpcs.Add(playerNpcDataWrap);
			}
		}
		mStarGodNpcs.Sort((x, y) => PartnerStarGodListRules.Compare(x, y, services.CanPinNpc));
	}

	private void RefreshList()
	{
		InitNpcList();
		SetConetntAnchor(isCenterShow: false);
		mLoop.dataSource = this;
		mLoop.totalCount = (int)Math.Ceiling((double)mStarGodNpcs.Count / (double)mTemplateGroupItem.GroupMaxCount);
		mLoop.RefillCells();
		SetConetntAnchor(mStarGodNpcs.Count <= 1);
		((Transform)mContent).localPosition = Vector3.zero;
	}

	private void SetConetntAnchor(bool isCenterShow)
	{
		if (isCenterShow)
		{
			mContent.anchorMin = new Vector2(0f, 0.5f);
			mContent.anchorMax = new Vector2(1f, 0.5f);
			mContent.pivot = new Vector2(0.5f, 0.5f);
		}
		else
		{
			mContent.anchorMin = new Vector2(0f, 1f);
			mContent.anchorMax = new Vector2(1f, 1f);
			mContent.pivot = new Vector2(0.5f, 1f);
		}
	}

	public void ProvideData(Transform transform, int idx)
	{
		StarGodNpcItemGroupItem component = ((Component)transform).GetComponent<StarGodNpcItemGroupItem>();
		if (!((Object)(object)component == (Object)null))
		{
			int num = idx * mTemplateGroupItem.GroupMaxCount;
			int num2 = Mathf.Min(num + mTemplateGroupItem.GroupMaxCount, mStarGodNpcs.Count);
			List<NpcRosterItem> list = new List<NpcRosterItem>();
			for (int i = num; i < num2; i++)
			{
				list.Add(mStarGodNpcs[i]);
			}
			component.Init(list, idx == 0);
		}
	}
}

}
