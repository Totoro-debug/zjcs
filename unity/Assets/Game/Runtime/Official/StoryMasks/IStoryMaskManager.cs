// Migrated selected original; see story-mask-runtime-source-manifest.json.
using System;
using Zjcs.Official.StoryBubbles;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
namespace Zjcs.Official.StoryMasks
{
using Object = UnityEngine.Object;
using UnityEngine;
using UnityEngine.UI;

public interface IStoryMaskManager
{
	void Enable();

	void Disable();

	void Reset();

	void CloseAllMask();

	void SetInvisibleMask(bool isOn);

	void SetColorMask(Color color = default(Color));

	void CloseColorMask();

	void PlayNoStoryColorMask(Color color, bool overrideSort = true);

	void CloseNoStoryColorMask();

	bool HasAnyMaskPlaying();

	bool CanShowMask();

	void OpenRectMask(RectTransform transform, MaskSetting setting);

	void OpenRectMask(GameObject go, MaskSetting setting);

	void OpenRectMask(Button btn, MaskSetting setting);

	void OpenRectMask(GameObject gameObject, Vector3 offset, Vector3 scale, Vector3 rectWLeftBottom, Vector3 rectWRightTop);

	void OpenRectMask(GameObject targetGo, MaskSetting setting, Vector2 customSize = default(Vector2));

	void CloseRectMask();

	void OpenClickCallMask(Action clickCb);

	void CloseClickCallMask();

	RectTransform GetButtonApplyMaskRect(Button button);

	Vector2 GetBoundSize(GameObject go, AttachToObjUI attachToObjUI);
}

}
