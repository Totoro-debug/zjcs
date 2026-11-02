// Selected original HUD path; see mainline-minimap-hud-code-source-manifest.json.
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Zjcs.Official.World.Map13MiniMap
{
public class MiniMapMaskedUI : MapMeCharacterUIBase
{
	public Button MiniMapBtn;

	[SerializeField]
	private RawImage mRawImage;

	public MiniMapHudContext Context { get; set; }
	public System.Func<RenderTexture> GetMiniMapTexture { get; set; }

	private void Awake()
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Expected Obj, but got Unknown
		((UnityEvent)MiniMapBtn.onClick).AddListener((UnityAction)OnClickMiniMap);
	}

	protected override void Start()
	{
		base.Start();
		mRawImage.texture = (Texture)(object)GetMiniMapTexture();
	}

	protected override void OnDestroy()
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Expected Obj, but got Unknown
		base.OnDestroy();
		((UnityEvent)MiniMapBtn.onClick).RemoveListener((UnityAction)OnClickMiniMap);
	}

	private void OnClickMiniMap()
	{
		Context.PopupFullScreen((IMiniMapHudFullScreenPanel panel) =>
		{
			panel.Init(Context.CurrentMapClassId);
		});
	}
}
}
