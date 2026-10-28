// Migrated selected official source; see map14-story-playable-runtime source receipt.
using Object = UnityEngine.Object;
using System;
using UnityEngine;

namespace Zjcs.Official.StoryPlayables
{
	public class MyPlayableApplyAttribute : Attribute
	{
		public Type[] myTypes;

		public MyPlayableApplyAttribute(params Type[] types)
		{
			myTypes = types;
		}

		public Component GetApplyComponent(MyPlayable playable, GameObject go)
		{
			if ((Object)(object)go == (Object)null)
			{
				return null;
			}
			Type[] array = myTypes;
			foreach (Type type in array)
			{
				Component component = go.GetComponent(type);
				if ((Object)(object)component != (Object)null)
				{
					return component;
				}
			}
			return null;
		}
	}
}
