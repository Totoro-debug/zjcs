namespace Zjcs.Official.BattleThemeUI
{
using Object = UnityEngine.Object;
using System;
using UnityEngine;

public class FightMainPanelThemeNpcModule : MonoBehaviour
{
	[SerializeField]
	private Transform mThemeNpcSlot;

    private string themePrefabPath;
    private Action<Transform> destroyChildren;
    private Action<string, Transform> instantiate;

    public void Configure(Transform slot, string prefabPath, Action<Transform> clear,
        Action<string, Transform> create)
    {
        mThemeNpcSlot = slot;
        themePrefabPath = prefabPath;
        destroyChildren = clear ?? throw new ArgumentNullException(nameof(clear));
        instantiate = create ?? throw new ArgumentNullException(nameof(create));
    }

	public void OnActivate()
	{
		Refresh();
	}

	private void Refresh()
	{
		if (!((Object)(object)mThemeNpcSlot == (Object)null))
		{
			destroyChildren(mThemeNpcSlot);
			if (themePrefabPath != null)
			{
				instantiate(themePrefabPath, mThemeNpcSlot);
			}
		}
	}
}

}
