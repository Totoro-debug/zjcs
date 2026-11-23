// Selected original home binding implementation; see the source receipt.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
using Newtonsoft.Json;
using Zjcs.Official.ProfessionUI;
namespace Zjcs.Official.HomeEntityBindings
{
using Object = UnityEngine.Object;
public class HomeEntityDataProvider : MonoBehaviour
{
	[SerializeField]
	private List<HomeEntityData> mDataList;

	private Dictionary<Type, List<HomeEntityData>> mDataDic;

	public Dictionary<Type, List<HomeEntityData>> DataDic
	{
		get
		{
			if (mDataDic == null)
			{
				mDataDic = new Dictionary<Type, List<HomeEntityData>>();
				int i = 0;
				for (int count = mDataList.Count; i < count; i++)
				{
					HomeEntityData homeEntityData = mDataList[i];
					if ((Object)(object)homeEntityData == (Object)null)
					{

						Debug.LogError("Official HomeEntityDataProvider.DataDic");
					}
					else
					{
						if (!mDataDic.TryGetValue(((object)homeEntityData).GetType(), out var value))
						{
							value = new List<HomeEntityData>();
							mDataDic.Add(((object)homeEntityData).GetType(), value);
						}
						value.Add(homeEntityData);
					}
				}
			}
			return mDataDic;
		}
	}
}
}
