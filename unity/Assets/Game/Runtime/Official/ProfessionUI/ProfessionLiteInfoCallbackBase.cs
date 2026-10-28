namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System;
using UnityEngine;

public abstract class ProfessionLiteInfoCallbackBase<T> : IDisposable
{
	private const string kGameOBjectDestroyedMsg = "UnityEngine.MissingReferenceException: The object of type";

	private Action<T> mCallback;

	public bool Valid { get; private set; }

	public ProfessionLiteInfoCallbackBase(Action<T> callback)
	{
		mCallback = callback;
		Valid = callback != null;
	}

	public void Invoke(T data)
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
            if (!ex.ToString().StartsWith(kGameOBjectDestroyedMsg))
            {
                Debug.LogException(ex);
                return;
            }
            Debug.LogError(mCallback.Method.Name + "未释放");
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
