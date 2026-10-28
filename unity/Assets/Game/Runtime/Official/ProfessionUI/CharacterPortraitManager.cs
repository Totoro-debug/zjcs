namespace Zjcs.Official.ProfessionUI
{
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

public class CharacterPortraitManager : ICharacterPortraitManager, ICharacterPortraitSkinHashProvider, IDisposable
{
	private const float kMemoryClearTimeDelay = 10f;

	private const int kTextureClearMark = 3;

	private readonly Queue<UniTaskCompletionSource> mWaitingQueue = new Queue<UniTaskCompletionSource>();

	private readonly Dictionary<long, CharacterPortraitCache> mCaches = new Dictionary<long, CharacterPortraitCache>();

	private CharacterPortraitTexturePool mTexturePool;

	private IProfessionPortraitStudio mPhotographer;

	private float mMemoryClearThink;

	private int mTextureClearMark;

	private readonly List<long> mCacheClearTempList = new List<long>();

	private ProfessionPortraitCancellation mDestroyToken;

	private Dictionary<int, long> mSkinIdToHash;

    private readonly IProfessionPortraitManagerBindings mEnvironment;

    public CharacterPortraitManager(IProfessionPortraitManagerBindings environment)
    { mEnvironment = environment ?? throw new ArgumentNullException(nameof(environment)); }

	public void Initialize()
	{

		mTextureClearMark = 3;
		mTexturePool = new CharacterPortraitTexturePool(mEnvironment.LowMemory);
		mPhotographer = mEnvironment.InstantiateStudio();
		Object.DontDestroyOnLoad(mPhotographer.Instance);
		mPhotographer.Init(mTexturePool);
		mDestroyToken = new ProfessionPortraitCancellation();
	}

	public void Dispose()
	{
		mDestroyToken.Dispose();
		mDestroyToken = null;
		mTexturePool.Dispose();
		mTexturePool = null;
		Object.Destroy(mPhotographer.Instance);
	}

	CharacterPortraitCache ICharacterPortraitManager.GetCharacterPortrait(ICharacterPortraitRequestor requestor)
	{

		long requestId = requestor.GetRequestId();
		if (!mCaches.TryGetValue(requestId, out var value))
		{
			value = new CharacterPortraitCache();
			UpdateCachaTexture(value, mEnvironment.Settings.LoadingTexture, -1L, needRecycle: false);
			mCaches.Add(requestId, value);
		}
		UniTaskExtensions.Forget(CaptureAgent(requestor, mDestroyToken.Token));
		return value;
	}

	public void ClearAllCharacterPortraitPersistenceData()
	{
		if (mEnvironment.Persister != null)
		{
			mEnvironment.Persister.ClearAllStorage();
		}
	}

    private async UniTask CaptureAgent(ICharacterPortraitRequestor requestor, CancellationToken cancellationToken)
    {
        try { await CaptureCharacterPortrait(requestor, cancellationToken); }
        catch (OperationCanceledException) { Debug.Log("任务被取消"); }
        catch (Exception exception) { Debug.LogException(exception); }
    }

    private async UniTask CaptureCharacterPortrait(ICharacterPortraitRequestor requestor, CancellationToken cancellationToken)
    {
        long requestId = requestor.GetRequestId();
        PlayerPortraitSettings setting = mEnvironment.Settings;
        requestor.GetDetailRequestData(setting, this, mEnvironment.AppearanceReader, out var data);
        CharacterPortraitCache cache = mCaches[requestId];
        if (cache.IsVaildCacha(in data)) return;
        if (mEnvironment.Persister.TryGetPersistentTexture(data, out var imageData) &&
            TryLoadTextureForBytes(requestor, imageData, out var stored))
        {
            UpdateCachaTexture(cache, stored, data.AppearanceHashCode, needRecycle: true);
            return;
        }
        if (!requestor.TryGetCancellationToken(out var token)) return;
        using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, token))
        {
            CancellationToken linkedToken = linkedCts.Token;
            var completion = new UniTaskCompletionSource();
            mWaitingQueue.Enqueue(completion);
            await completion.Task.AttachExternalCancellation(linkedToken);
            linkedToken.ThrowIfCancellationRequested();
        }
        requestor.GetDetailRequestData(setting, this, mEnvironment.AppearanceReader, out data);
        CharacterPortraitTexture2D texture = mPhotographer.CapturePortraitTexture(requestor, out var bytes);
        if (texture == null)
        {
            UpdateCachaTexture(cache, setting.DisableTexture, -1L, needRecycle: false);
            Debug.LogError("请求 " + data + " 拍摄失败");
        }
        else
        {
            UpdateCachaTexture(cache, texture, data.AppearanceHashCode, needRecycle: true);
            if (requestor.NeedPersist()) mEnvironment.Persister.SetPersistentTexture(data, bytes);
        }
    }

	private bool TryLoadTextureForBytes(ICharacterPortraitRequestor requestor, byte[] bsImageData, out CharacterPortraitTexture2D characterPortraitTexture)
	{

		if (bsImageData == null)
		{
			characterPortraitTexture = null;
			Debug.LogError("Original portrait cache diagnostic.");
			return false;
		}
		PlayerPortraitSettings.PhotographyParameters param = mEnvironment.Settings.GetParameters(requestor.GetPortraitType());
		characterPortraitTexture = GetTexture(param.TextureWidth, param.TextureHeight);
		characterPortraitTexture.LoadImage(bsImageData);
		if (characterPortraitTexture.Width != param.TextureWidth || characterPortraitTexture.Height != param.TextureHeight)
		{
			Debug.LogError("Original portrait cache diagnostic.");
			characterPortraitTexture.DestroyTexture();
			return false;
		}
		return true;
	}

	private void CheckAndRecycleCache()
	{

		float realtimeSinceStartup = Time.realtimeSinceStartup;
		foreach (KeyValuePair<long, CharacterPortraitCache> mCache in mCaches)
		{
			CharacterPortraitCache value = mCache.Value;
			if (value.IsExpire(realtimeSinceStartup))
			{
				if (value.NeedRecycle)
				{
					CharacterPortraitTexture2D characterPortraitTexture = mCache.Value.ForceGetTexture();
					RecycleTexture(characterPortraitTexture);
				}
				value.Clear();
				mCacheClearTempList.Add(mCache.Key);
			}
		}
		int count = mCacheClearTempList.Count;
		if (count > 0)
		{
			Debug.Log("Original portrait cache diagnostic.");
		}
		for (int i = 0; i < count; i++)
		{
			mCaches.Remove(mCacheClearTempList[i]);
		}
		mCacheClearTempList.Clear();
	}

	public void Update()
	{
		UniTaskCompletionSource val = default;
		while (mWaitingQueue.TryDequeue(out val))
		{
			val.TrySetResult();
		}
		float time = Time.time;
		if (!(mMemoryClearThink > time))
		{
			MemoryClear();
			mMemoryClearThink = time + 10f;
		}
	}

	private void MemoryClear()
	{
		CheckAndRecycleCache();
		if (mTextureClearMark-- <= 0)
		{
			mTexturePool.ReleaseResourcesCache();
			mTextureClearMark = 3;
		}
	}

	private CharacterPortraitTexture2D GetTexture(int width, int height)
	{
		return mTexturePool.GetTexture(width, height);
	}

	private void RecycleTexture(CharacterPortraitTexture2D characterPortraitTexture)
	{

		if (characterPortraitTexture == null)
		{
			Debug.LogError("Original portrait cache diagnostic.");
		}
		else
		{
			mTexturePool.Recycle(characterPortraitTexture);
		}
	}

	private void UpdateCachaTexture(CharacterPortraitCache cache, CharacterPortraitTexture2D tex, long hashCode, bool needRecycle)
	{
		bool needRecycle2 = cache.NeedRecycle;
		cache.UpdateTexture(tex, hashCode, needRecycle, out var oldTexture);
		if ((oldTexture != null) & needRecycle2)
		{
			RecycleTexture(oldTexture);
		}
	}

	private void InitSkinToHashDict()
	{
		mSkinIdToHash = new Dictionary<int, long>();
		IReadOnlyList<ProfessionPortraitSkinHash> appearanceToHash = mEnvironment.AppearanceHashes;
		Dictionary<string, long> dictionary = new Dictionary<string, long>(appearanceToHash.Count);
		foreach (ProfessionPortraitSkinHash item in appearanceToHash)
		{
			dictionary.TryAdd(item.SkinName, item.HashValue);
		}
		IReadOnlyList<ProfessionPortraitSkinName> all = mEnvironment.SkinNames;
		foreach (ProfessionPortraitSkinName spineSkinInfo in all)
		{
			string skinName = spineSkinInfo.SkinName;
			long value;
			if (skinName == null)
			{
				Debug.LogWarning("Original portrait skin hash lookup failed.");
			}
			else if (dictionary.TryGetValue(skinName, out value))
			{
				mSkinIdToHash.Add(spineSkinInfo.Id, value);
			}
		}
	}

	public long GetSkinHash(int skinId)
	{
		if (mSkinIdToHash == null)
		{
			InitSkinToHashDict();
		}
		if (!mSkinIdToHash.TryGetValue(skinId, out var value))
		{
			Debug.LogError("Original portrait skin hash lookup failed.");
			return 0L;
		}
		return value;
	}
}

}
