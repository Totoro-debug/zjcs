namespace Zjcs.Official.ProfessionUI
{
public struct CharacterPortraitDetailRequestData
{
	public long RequestId;

	public long AppearanceHashCode;

	public long VersionId;

	public override readonly string ToString()
	{
		return $"{base.ToString()} RequestId: {RequestId} AppearanceHashCode: {AppearanceHashCode} VersionId {VersionId}";
	}
}

}
