// Migrated from selected original language code; see Map13 language source receipt.
namespace Zjcs.Official.World.NativeLabelUI
{
using System.Collections.Generic;
using Zjcs.Official.World.NativeLabelUI.Framework;

public class GameLanguageBinaryTextDataProvider : ILanguageTextDataProvider
{
	public List<string> FilePaths { get; } = new List<string> { "Binary/Language/{0}/text.g.bytes" };

	public GameLanguageBinaryTextDataProvider(string rootDir)
	{
		rootDir = (string.IsNullOrEmpty(rootDir) ? string.Empty : (rootDir.EndsWith('/') ? rootDir : (rootDir + "/")));
		for (int i = 0; i < FilePaths.Count; i++)
		{
			FilePaths[i] = rootDir + FilePaths[i];
		}
	}
}

}
