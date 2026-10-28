namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System;
using UnityEngine;

public interface ICharacterPartEntryProvider
{
	CharacterPartPoolEntry RentIntegratedPart(SkinType skinType, string skinName, Transform parent, bool usePool, Action<GameObject> onBeforeSetParent);

	CharacterPartPoolEntry RentStandalonePart(SkinType skinType, string skinName, Transform parent, bool isUI, bool usePool);

	void ReturnPart(CharacterPartPoolEntry entry, bool tryUnloadAllAssets);
}

}
