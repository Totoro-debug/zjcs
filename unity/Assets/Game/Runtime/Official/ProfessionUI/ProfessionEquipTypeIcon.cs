namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using Zjcs.Official.WorldUI;
using UnityEngine;
using UnityEngine.UI;

public class ProfessionEquipTypeIcon : MonoBehaviour
{
    private IProfessionInfoBindings mBindings;
    private IProfessionInfoBindings Bindings => mBindings ?? ProfessionInfoBindings.Required;
    public void Configure(IProfessionInfoBindings bindings)
    { mBindings = bindings ?? throw new System.ArgumentNullException(nameof(bindings)); }

	public Image Icon;

	public WorldMultiLanguageText Name;

	private ProfessionResourceOperationBag mDisposableBag;


	private void Awake()
	{
		mDisposableBag = new ProfessionResourceOperationBag();
	}

	private void OnDestroy()
	{
		mDisposableBag.Dispose();
	}

	public void RefeshView(int itemType)
	{
		Icon.sprite = mDisposableBag.Add(Bindings.ResourceLoader.LoadAsset<Sprite>(Bindings.GetEquipTypeIcon(itemType))).Result;
		Name.SetKey(Bindings.GetEquipTypeNameKey(itemType));
	}
}

}
