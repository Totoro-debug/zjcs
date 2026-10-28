namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using TMPro;

public interface IFontAssetFontService
{
	void Initialize();

	void SetupFont(TMP_FontAsset fontAsset);

	void ReleaseFont(string name);

	string GetSourceFontFileName(TMP_FontAsset fontAsset);
}

}
