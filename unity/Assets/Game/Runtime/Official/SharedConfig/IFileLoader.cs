namespace GConfig
{
    public interface IFileLoader
    {
        void Initialize(ConfigManager manager);
        void LoadFile(string file, bool isBinary, bool async, ConfigRequestBase request);
        bool Exists(string file);
    }
}
