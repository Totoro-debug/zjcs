// Migrated from selected original language code; see Map13 language source receipt.
namespace Zjcs.Official.World.NativeLabelUI
{
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

public class LocalizationBinaryFileReader : IDisposable
{
	private const int kHeaderSize = 4;

	private const int kIndexEntrySize = 12;

	private const int kHashSize = 8;

	private const int kTextLengthSize = 2;

	private const int kSharedLengthBufferSize = 8;

	private const int kInitialTextBufferSize = 256;

	private Stream mStream;

	private readonly Dictionary<ulong, int> mHashToOffsetMap;

	private readonly int mTextDataStartOffset;

	private bool mDisposed;

	private bool mIsValid;

	private readonly byte[] mSharedLengthBuffer = new byte[8];

	private byte[] mSharedTextBuffer = new byte[256];

	public LocalizationBinaryFileReader(string filePath)
	{
		mDisposed = false;
		mIsValid = false;
		mHashToOffsetMap = new Dictionary<ulong, int>();
		mTextDataStartOffset = 0;
		mStream = new MemoryStream();
		if (string.IsNullOrEmpty(filePath))
		{
			UnityEngine.Debug.LogError("Original localization binary read error");
			return;
		}
		if (!File.Exists(filePath))
		{
			UnityEngine.Debug.LogError("Original localization binary read error");
			return;
		}
		try
		{
			mStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
			if (mStream == null)
			{
				UnityEngine.Debug.LogError("Original localization binary read error");
			}
			else
			{
				SafeRead(mStream, mSharedLengthBuffer, 0, 4);
				int num = BitConverter.ToInt32(mSharedLengthBuffer, 0);
				mHashToOffsetMap = new Dictionary<ulong, int>(num);
				mTextDataStartOffset = 4 + num * 12;
				LoadHashToOffsetMap(num);
				mIsValid = true;
			}
		}
		catch (Exception ex)
		{
			UnityEngine.Debug.LogError("Original localization binary read error");
			mStream?.Dispose();
			mStream = null;
		}
	}

	public bool TryGetTextByKey(string key, out string result)
	{
		result = null;
		if (string.IsNullOrEmpty(key))
		{
			return false;
		}
		ulong hashCode = XXHash64.ComputeHash(key, 0uL);
		result = GetTextByHash(hashCode);
		return result != null;
	}

	public bool ContainKey(string key)
	{
		int value;
		return mHashToOffsetMap.TryGetValue(GetHashCodeByKey(key), out value);
	}

	public ulong GetHashCodeByKey(string key)
	{
		return XXHash64.ComputeHash(key, 0uL);
	}

	public string? GetTextByHash(ulong hashCode)
	{
		if (!mIsValid || mStream == null)
		{
			return null;
		}
		if (!mHashToOffsetMap.TryGetValue(hashCode, out var value))
		{
			return null;
		}
		try
		{
			int num = mTextDataStartOffset + value;
			mStream.Seek(num, SeekOrigin.Begin);
			SafeRead(mStream, mSharedLengthBuffer, 0, 2);
			ushort num2 = BitConverter.ToUInt16(mSharedLengthBuffer, 0);
			if (mSharedTextBuffer.Length < num2)
			{
				mSharedTextBuffer = new byte[num2];
			}
			SafeRead(mStream, mSharedTextBuffer, 0, num2);
			return Encoding.UTF8.GetString(mSharedTextBuffer, 0, num2);
		}
		catch (Exception ex)
		{
			UnityEngine.Debug.LogError("Original localization binary read error");
			return null;
		}
	}

	private void LoadHashToOffsetMap(int count)
	{
		if (mStream == null)
		{
			UnityEngine.Debug.LogError("Original localization binary read error");
			return;
		}
		try
		{
			mStream.Seek(4L, SeekOrigin.Begin);
			byte[] array = new byte[count * 12];
			SafeRead(mStream, array, 0, array.Length);
			for (int i = 0; i < count; i++)
			{
				int num = i * 12;
				ulong key = BitConverter.ToUInt64(array, num);
				int value = BitConverter.ToInt32(array, num + 8);
				mHashToOffsetMap[key] = value;
			}
		}
		catch (Exception ex)
		{
			UnityEngine.Debug.LogError("Original localization binary read error");
			mHashToOffsetMap.Clear();
		}
	}

	private void SafeRead(Stream stream, byte[] buffer, int offset, int count)
	{
		if (stream == null)
		{
			UnityEngine.Debug.LogError("Original localization binary read error");
			return;
		}
		int num;
		for (int i = 0; i < count; i += num)
		{
			num = stream.Read(buffer, offset + i, count - i);
			if (num == 0)
			{
				UnityEngine.Debug.LogError("Original localization binary read error");
				break;
			}
		}
	}

	public void Dispose()
	{
		Dispose(disposing: true);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!mDisposed)
		{
			if (disposing)
			{
				mStream?.Dispose();
			}
			mDisposed = true;
		}
	}
}

}
