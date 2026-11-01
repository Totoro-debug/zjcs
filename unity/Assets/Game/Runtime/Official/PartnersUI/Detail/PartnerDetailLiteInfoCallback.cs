// Selected LiteInfoCallbackBase; only logging and closed generic type adapted.
namespace Zjcs.Official.PartnersUI
{
using System;

public sealed class PartnerDetailLiteInfoCallback : IDisposable
{
	private const string kGameOBjectDestroyedMsg = "UnityEngine.MissingReferenceException: The object of type";

	private Action<PartnerDetailLiteInfo> mCallback;

	public bool Valid { get; private set; }

	public PartnerDetailLiteInfoCallback(Action<PartnerDetailLiteInfo> callback)
	{
		mCallback = callback;
		Valid = callback != null;
	}

	public void Invoke(PartnerDetailLiteInfo data)
	{
		if (!Valid)
		{
			return;
		}
		try
		{
			mCallback(data);
		}
		catch (Exception ex)
		{
            if (!ex.ToString().StartsWith("UnityEngine.MissingReferenceException: The object of type"))
            {
                UnityEngine.Debug.LogException(ex);
                return;
            }
            UnityEngine.Debug.LogError(mCallback.Method.Name + "未释放");
			Release();
		}
	}

	public void Release()
	{
		if (Valid)
		{
			((IDisposable)this).Dispose();
		}
	}

	public override string ToString()
	{
		if (mCallback != null)
		{
			return mCallback.Method.Name;
		}
		return string.Empty;
	}

	void IDisposable.Dispose()
	{
		Valid = false;
		mCallback = null;
	}
}

}
