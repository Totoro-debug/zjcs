using System;

namespace GConfig
{
    public enum ConfigManagerStatus { BeforeParsingLaunch, ParsingLaunch, BeforeParsingGame, ParsingGame, ParsePhaseFinished }
    public partial class ConfigManager
    {
        private ConfigManagerStatus status;
        public ConfigManagerStatus Status => status;
        private void InitializeCommon(string rootDir)
        {
            if (string.IsNullOrEmpty(rootDir)) rootDir = this.rootDir;
            if (string.IsNullOrEmpty(rootDir)) rootDir = string.Empty;
            else if (!rootDir.EndsWith("/")) rootDir += "/";
            this.rootDir = rootDir;
        }
        public void Initialize(string rootDir = "", bool supportAsyncParsing = true, int threadCount = 0)
        {
            InitializeCommon(rootDir);
            fileLoader = GConfig.FileLoader.sFileLoader;
            fileLoader.Initialize(this);
            this.supportAsyncParsing = supportAsyncParsing;
            if (supportAsyncParsing) threadPool = new ThreadPool(threadCount);
        }
        public void ResetThreadPool(int threadCount)
        {
            if (status != ConfigManagerStatus.BeforeParsingLaunch && status != ConfigManagerStatus.BeforeParsingGame && status != ConfigManagerStatus.ParsePhaseFinished)
                throw new Exception("当前状态不能修改线程池: " + status);
            if (threadCount > 0)
            {
                if (supportAsyncParsing) threadPool.Dispose();
                supportAsyncParsing = true;
                threadPool = new ThreadPool(threadCount);
            }
            else if (supportAsyncParsing)
            {
                supportAsyncParsing = false;
                threadPool.Dispose();
                threadPool = null;
            }
        }
    }
}
