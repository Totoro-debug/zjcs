public interface IVersionManager
{
	string Version { get; }

	int ResVersion { get; set; }

	int CodeVersion { get; set; }

	void Reload();
}
