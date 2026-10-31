// Migrated from selected official sources; see partner-ui-helpers-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Object = UnityEngine.Object;
using TabGroup = Zjcs.Official.WorldUI.WorldTabGroup;
using TabItem = Zjcs.Official.WorldUI.WorldTabItem;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using LanguageUtils = Zjcs.Official.PartnersUI.PartnerUiLanguage;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ListItemShowSequence : MonoBehaviour
{
	[SerializeField]
	private bool mIsAutoShow = true;

	[SerializeField]
	private float mShowInterval = 0.06f;

	[SerializeField]
	private float mFirstShowDelay;

	private Coroutine mShowCoroutine;

	private WaitForSeconds mWaitForSeconds;

	private bool mHasAppliedFirstShowDelay;

	private LinkedList<IShowSequenceItem> mShowList = new LinkedList<IShowSequenceItem>();

	private Dictionary<IShowSequenceItem, LinkedListNode<IShowSequenceItem>> mItemNodeMap = new Dictionary<IShowSequenceItem, LinkedListNode<IShowSequenceItem>>();

	public float ShowInterval => mShowInterval;

	private void OnDisable()
	{
		TryStopShow();
		mHasAppliedFirstShowDelay = false;
		mShowList.Clear();
		mItemNodeMap.Clear();
	}

	public void AddItem(IShowSequenceItem item)
	{
		if (mItemNodeMap.TryGetValue(item, out var value))
		{
			mShowList.Remove(value);
		}
		LinkedListNode<IShowSequenceItem> value2 = mShowList.AddLast(item);
		mItemNodeMap[item] = value2;
		item.Hide();
		if (mIsAutoShow)
		{
			TryStartShow();
		}
	}

	public void RemoveItem(IShowSequenceItem item)
	{
		if (mItemNodeMap.TryGetValue(item, out var value))
		{
			mShowList.Remove(value);
			mItemNodeMap.Remove(item);
		}
	}

	public void StartShow()
	{
		TryStartShow();
	}

	public void StopShow()
	{
		TryStopShow();
	}

	public bool CanShow()
	{
		return mShowList.Count > 0;
	}

	private void TryStartShow()
	{
		if (mShowCoroutine == null)
		{
			mShowCoroutine = ((MonoBehaviour)this).StartCoroutine(Show());
		}
	}

	private void TryStopShow()
	{
		if (mShowCoroutine != null)
		{
			((MonoBehaviour)this).StopCoroutine(mShowCoroutine);
			mShowCoroutine = null;
		}
	}

	private IEnumerator Show()
	{
		if (!mHasAppliedFirstShowDelay && mFirstShowDelay > 0f)
		{
			yield return (object)new WaitForSeconds(mFirstShowDelay);
		}
		mHasAppliedFirstShowDelay = true;
		if (mWaitForSeconds == null)
		{
			mWaitForSeconds = new WaitForSeconds(mShowInterval);
		}
		while (CanShow())
		{
			yield return mWaitForSeconds;
			if (!CanShow())
			{
				break;
			}
			IShowSequenceItem value = mShowList.First.Value;
			mShowList.RemoveFirst();
			mItemNodeMap.Remove(value);
			value.Show();
		}
		mShowCoroutine = null;
	}
}

}
