// Official client 884_156692; original request/handler control flow.
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.BattleRoundUI.DynamicTexture2DAtlas;
namespace Zjcs.Official.BattleRoundUI
{
using Object = UnityEngine.Object;
using UnityEngine;

public class FightDynamicTextureETC2 : System.IDisposable, IFightDynamicTextureETC2
{
	private IGridDynamicAtlas mDynamicAtlas;

	private const int kAtlasWidth = 1024;

	private const int kAtlasHeight = 1024;

	private static readonly Vector2Int sGrideSize = new Vector2Int(100, 120);

	private GamePlayManagerConfig mGamePlayConfig { get; set; }

	public void BindConfiguration(GamePlayManagerConfig config) => mGamePlayConfig = config ?? throw new System.ArgumentNullException(nameof(config));

	private System.Func<string, Texture2D> mResourceLoader;

	public void Initialize(GamePlayManagerConfig config, System.Func<string, Texture2D> resourceLoader)
	{
		BindConfiguration(config);
		mResourceLoader = resourceLoader ?? throw new System.ArgumentNullException(nameof(resourceLoader));
		if (mGamePlayConfig.EnablePlayerPortraitETC2)
		{
			mDynamicAtlas = DynamicTexture2DAtlasUtils.CreateGridDynamicAtlas(1024, 1024, sGrideSize, null, needCreateSprite: true);
			AddFactionTextures();
		}
	}

	public void Dispose()
	{
		mDynamicAtlas?.Dispose();
		mDynamicAtlas = null;
	}

	public AddTexture2DResult Add(Texture2D tex, long uniqueKey)
	{
		if (mDynamicAtlas == null)
		{
			return AddTexture2DResult.Failed;
		}
		return mDynamicAtlas.Add(tex, uniqueKey);
	}

	public void ClearAllSprites()
	{
		mDynamicAtlas?.ClearAllSprites();
	}

	public GridSpriteInfo Get(long uniqueKey)
	{
		if (mDynamicAtlas == null)
		{
			return null;
		}
		return mDynamicAtlas.Get(uniqueKey);
	}

	public GridSpriteInfo TryGetOrAdd(Texture2D tex, long uniqueKey)
	{
		if ((Object)(object)tex == (Object)null || !((Texture)tex).isReadable)
		{
			return null;
		}
		if (Contains(uniqueKey))
		{
			return Get(uniqueKey);
		}
		return Add(tex, uniqueKey).SpriteInfo;
	}

	public bool Contains(long uniqueKey)
	{
		if (mDynamicAtlas == null)
		{
			return false;
		}
		return mDynamicAtlas.ContainsKey(uniqueKey);
	}

	public bool Remove(long uniqueKey)
	{
		if (mDynamicAtlas == null)
		{
			return false;
		}
		return mDynamicAtlas.Remove(uniqueKey);
	}

	private void AddFactionTextures()
	{
		GamePlayManagerConfig value = mGamePlayConfig;
		AddFactionTexture(value.DefaultEffect);
		AddFactionTexture(value.CharacterMeEffect);
		AddFactionTexture(value.SpactatedCharacterEffect);
		foreach (GameEffectData factionEffectData in value.FactionEffectDatas)
		{
			AddFactionTexture(factionEffectData);
		}
	}

	private void AddFactionTexture(GameEffectData data)
	{
		Texture2D result = mResourceLoader(data.FactionRoundImgETC2Path);
		if (!((Object)(object)result == (Object)null))
		{
			int hashCode = data.FactionRoundImgETC2Path.GetHashCode();
			if (!mDynamicAtlas.ContainsKey(hashCode))
			{
				mDynamicAtlas.Add(result, hashCode);
			}
		}
	}

	static FightDynamicTextureETC2()
	{
	}
}

}
