// Migrated from selected original language code; see Map13 language source receipt.
namespace Zjcs.Official.World.NativeLabelUI.Framework
{

public sealed class CultureTextArg : ArgBase
{
	private readonly bool mNeedTranslation;

	public string KeyOrText { get; }

	public object[] Args { get; }

	public CultureTextArg(string key, params object[] args)
		: this(key, args, needTranslation: true)
	{
	}

	public CultureTextArg(string keyOrText, object[] args, bool needTranslation)
	{
		KeyOrText = keyOrText;
		Args = args;
		mNeedTranslation = needTranslation;
	}

	public override string ToString()
	{
		if (!mNeedTranslation)
		{
			return KeyOrText;
		}
		if (Args != null && Args.Length != 0)
		{
			return Culture.Text.GetTextFormatted(KeyOrText, Args);
		}
		return Culture.Text.GetText(KeyOrText);
	}
}

}
