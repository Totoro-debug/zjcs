// Migrated from selected official managed code; see Map13 label-core source receipt.
namespace Zjcs.Official.World.NativeLabelUI.Framework
{
using Object = UnityEngine.Object;
using System;
using System.Collections.Generic;


public interface ITextReplacer
{
	string ReplaceText(string text, params object[] paras);

	bool TryMatchReplaceKey(string text, out HashSet<string> keys);

	void Register(string methodName, Func<object> method);

	void Unregister(string methodName);
}

}
