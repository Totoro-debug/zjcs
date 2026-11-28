// Complete original common promotion control; explicit local material services.
using System;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
namespace Zjcs.Official.ProfessionUI {
using Object=UnityEngine.Object;
using System;



using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class ProfessionUseItemSliderGroup : MonoBehaviour, IGameObjectPoolItem
{
    private IProfessionMaterialServices services;
    private IProfessionMaterialServices Services => services??throw new InvalidOperationException("Bind original material controls before activation.");
    public void Configure(IProfessionMaterialServices value)=>services=value??throw new ArgumentNullException(nameof(value));

	[SerializeField]
	private Transform myItemSlot;

	[SerializeField]
	private Slider mySlider;

	[SerializeField]
	private WorldMultiLanguageText myNameTxt;

	[SerializeField]
	private WorldMultiLanguageText myCountTxt;

	public Action<int, long> OnValueChangedEvent;

	private int _itemId;

	private long _maxCount;

	private ItemIcon _itemIcon;


	public long UseCount => (long)mySlider.value;

	public int ItemId => _itemId;

	private void Awake()
	{
		((UnityEvent<float>)(object)mySlider.onValueChanged).AddListener((UnityAction<float>)OnValueChanged);
	}

	private void OnDisable()
	{
		OnValueChangedEvent = null;
		if (services != null && (Object)(object)_itemIcon != (Object)null)
		{
			Services.RecycleIcon(_itemIcon);
		}
	}

	private void OnValueChanged(float value)
	{
		myCountTxt.SetKey("{0}/{1}", value, (long)mySlider.maxValue);
		OnValueChangedEvent?.Invoke(_itemId, (long)value);
	}

	public void Init(int itemId, long useCount, long maxCount)
	{
		_itemId = itemId;
		_maxCount = maxCount;
		_itemIcon = Services.GetIcon(itemId, myItemSlot).SetClickPopupInfoDialog().HideText();
		myNameTxt.SetKey(Services.GetItemNameKey(itemId));
		mySlider.maxValue = _maxCount;
		SetCountWithNotify(useCount);
	}

	public void UpdateMaxCount(long maxCount)
	{
		mySlider.maxValue = maxCount;
		SetCountWithNotify(maxCount);
	}

	public void SetCountWithNotify(long count)
	{
		mySlider.SetValueWithoutNotify((float)count);
		myCountTxt.SetKey("{0}/{1}", CultureNumberArg.Compact(count), CultureNumberArg.Compact((long)mySlider.maxValue));
	}

	public void OnGameObjectPoolReset()
	{
	}

	public void OnGameObjectPoolClear()
	{
	}
}

}
