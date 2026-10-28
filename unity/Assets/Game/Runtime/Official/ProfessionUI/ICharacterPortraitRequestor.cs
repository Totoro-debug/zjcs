namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System.Threading;
using UnityEngine;

public interface ICharacterPortraitRequestor
{
	long GetRequestId();

	object GetAppearanceData();

	ProfessionPortraitKind GetPortraitType();

	int GetSex();

	int GetProfession();

	string GetAnimationName();

	bool NeedPersist();

	bool TryGetCancellationToken(out CancellationToken cancellationToken);

	void OnTextureUpdate(Texture2D texture);

	void StopRequest();
}

}
