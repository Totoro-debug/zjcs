using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
namespace Zjcs.Official.World.NativeLabelUI.Framework {
// Original TextReplacer core; dynamic gameplay methods are registered by their owners.
public sealed class WorldTextReplacer:ITextReplacer {
    private readonly Func<string,string> getText;
    private readonly string mReplacePattern="\\{(.*?)\\}";
    private readonly Dictionary<string,Func<object>> mDynamicMethodDict=new Dictionary<string,Func<object>>();
    public WorldTextReplacer(Func<string,string> getText){this.getText=getText??throw new ArgumentNullException(nameof(getText));}
    private string GetReplacementString(string key,params object[] paras){
        if(mDynamicMethodDict.TryGetValue(key,out var method)){
            object value=method();if(value!=null)return value.ToString();
            Debug.LogError("replace method return null!key:"+key);return null;
        }
        return getText(key);
    }
public string ReplaceText(string text, params object[] paras)
	{
		StringBuilder stringBuilder = new StringBuilder(100);
		int i = 0;
		for (int length = text.Length; i < length; i++)
		{
			char c = text[i];
			if (c == '{')
			{
				for (int j = i + 1; j < length; j++)
				{
					if (text[j] == '}')
					{
						string val = text.Substring(i + 1, j - i - 1);
						if (ShouldIgnore(val))
						{
							stringBuilder.Append('{');
							stringBuilder.Append(val);
							stringBuilder.Append('}');
						}
						else
						{
							string key = val;
							string replacementString = GetReplacementString(key, paras);
							stringBuilder.Append(replacementString);
						}
						i = j;
						break;
					}
				}
			}
			else
			{
				stringBuilder.Append(c);
			}
		}
		return stringBuilder.ToString();
	}
private bool ShouldIgnore(string key)
	{
		if ("c" == key)
		{
			return true;
		}
		return IsAllDigits(key);
	}
private bool IsAllDigits(string input)
	{
		for (int i = 0; i < input.Length; i++)
		{
			if (!char.IsDigit(input[i]))
			{
				return false;
			}
		}
		return input.Length > 0;
	}
public bool TryMatchReplaceKey(string text, out HashSet<string> keys)
	{
		MatchCollection matchCollection = Regex.Matches(text, mReplacePattern);
		if (matchCollection.Count > 0)
		{
			keys = new HashSet<string>();
			for (int i = 0; i < matchCollection.Count; i++)
			{
				string value = matchCollection[i].Groups[1].Value;
				if (!ShouldIgnore(value))
				{
					keys.Add(value);
				}
			}
			return keys.Count > 0;
		}
		keys = null;
		return false;
	}
public void Register(string methodName, Func<object> method)
	{
		mDynamicMethodDict.Add(methodName, method);
	}
public void Unregister(string methodName)
	{
		mDynamicMethodDict.Remove(methodName);
	}
}}
