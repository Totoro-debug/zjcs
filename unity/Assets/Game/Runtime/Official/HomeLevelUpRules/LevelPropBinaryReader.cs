// Selected original 884 implementation; configuration and logging boundaries only.
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Zjcs.Official.HomeIncomeRules;
using Zjcs.Official.HomeExperienceRules;
namespace Zjcs.Official.HomeLevelUpRules
{

internal class LevelPropBinaryReader : IDisposable
{
	private struct ClassIndexEntry
	{
		public int MinLevel;

		public int MaxLevel;

		public int[] Offsets;
	}

	private class FileSectionInfo
	{
		public string FileName;

		public int FileOffset;

		public PropType[] PropTypes;
	}

	private const uint kMagicNumber = 1280328262u;

	private const ushort kVersion = 1;

	private Stream mStream;

	private BinaryReader mReader;

	private Dictionary<int, ClassIndexEntry> mIndex;

	private FileSectionInfo[] mFileSections;

	public IEnumerable<int> ClassIds => mIndex.Keys;

	public LevelPropBinaryReader(string filePath)
	{
		try
		{
			mStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
			mReader = new BinaryReader(mStream);
			ReadFileHeader();
			ReadIndex();
		}
		catch (Exception exception)
		{

			HomeLevelUpDiagnostics.Error("LevelPropBinaryReader original input error" + ": " + exception);
			Dispose();
			throw;
		}
	}

	public LevelPropBinaryReader(byte[] bytes)
	{
		try
		{
			mStream = new MemoryStream(bytes);
			mReader = new BinaryReader(mStream);
			ReadFileHeader();
			ReadIndex();
		}
		catch (Exception exception)
		{

			HomeLevelUpDiagnostics.Error("LevelPropBinaryReader original input error" + ": " + exception);
			Dispose();
			throw;
		}
	}

	private void ReadFileHeader()
	{
		if (mReader.ReadUInt32() != 1280328262)
		{
			throw new InvalidDataException("Invalid binary file format");
		}
		ushort num = mReader.ReadUInt16();
		if (num != 1)
		{
			throw new InvalidDataException($"Unsupported binary file version: {num}");
		}
		int num2 = mReader.ReadInt32();
		int num3 = mReader.ReadInt32();
		mStream.Seek(num2, SeekOrigin.Begin);
		ReadFileSections();
		mStream.Seek(num3, SeekOrigin.Begin);
	}

	private void ReadIndex()
	{
		int num = mReader.ReadInt32();
		mIndex = new Dictionary<int, ClassIndexEntry>(num);
		for (int i = 0; i < num; i++)
		{
			int key = mReader.ReadInt32();
			int num2 = mReader.ReadInt32();
			int num3 = mReader.ReadInt32();
			int num4 = num3 - num2 + 1;
			int[] array = new int[num4];
			for (int j = 0; j < num4; j++)
			{
				array[j] = mReader.ReadInt32();
			}
			mIndex[key] = new ClassIndexEntry
			{
				MinLevel = num2,
				MaxLevel = num3,
				Offsets = array
			};
		}
	}

	private void ReadFileSections()
	{
		int num = mReader.ReadInt32();
		mFileSections = new FileSectionInfo[num];
		for (int i = 0; i < num; i++)
		{
			ushort num2 = mReader.ReadUInt16();
			PropType[] array = new PropType[num2];
			for (int j = 0; j < num2; j++)
			{
				array[j] = (PropType)mReader.ReadUInt16();
			}
			mFileSections[i] = new FileSectionInfo
			{
				PropTypes = array
			};
		}
	}

	public bool TryGetProps(int classId, int level, out IPropDictionary props)
	{
		props = null;
		if (!mIndex.TryGetValue(classId, out var value))
		{
			return false;
		}
		if (level < value.MinLevel || level > value.MaxLevel)
		{
			return false;
		}
		int num = level - value.MinLevel;
		int num2 = value.Offsets[num];
		if (num2 == -1)
		{
			return false;
		}
		try
		{
			mStream.Seek(num2, SeekOrigin.Begin);
			int num3 = (int)ReadVarInt();
			FileSectionInfo fileSectionInfo = mFileSections[num3];
			props = ReadRowData(fileSectionInfo.PropTypes);
			return true;
		}
		catch (Exception exception)
		{

			HomeLevelUpDiagnostics.Error("LevelPropBinaryReader original input error" + ": " + exception);
		}
		return false;
	}

	public bool TryGetLevelRange(int classId, out int minLevel, out int maxLevel)
	{
		if (mIndex.TryGetValue(classId, out var value))
		{
			minLevel = value.MinLevel;
			maxLevel = value.MaxLevel;
			return true;
		}
		minLevel = 0;
		maxLevel = 0;
		return false;
	}

	private IPropDictionary ReadRowData(PropType[] propTypes)
	{
		IPropDictionary propDictionary = PropDictionaryUtils.Create();
		int num = propTypes.Length;
		for (int i = 0; i < num; i++)
		{
			long num2 = ReadVarInt();
			if (num2 != 0L)
			{
				propDictionary.Add(propTypes[i], num2);
			}
		}
		return propDictionary;
	}

	private long ReadVarInt()
	{
		ulong num = 0uL;
		int num2 = 0;
		byte b;
		do
		{
			b = mReader.ReadByte();
			num |= (ulong)((long)(b & 0x7F) << num2);
			num2 += 7;
		}
		while ((b & 0x80) != 0 && num2 < 64);
		long num3 = (long)(num >> 1);
		if ((num & 1) != 0L)
		{
			num3 = ~num3;
		}
		return num3;
	}

	public void Dispose()
	{
		mReader?.Dispose();
		mReader = null;
		mStream = null;
		mIndex?.Clear();
		mFileSections = null;
	}
}

}
