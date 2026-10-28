namespace GConfig.Parser
{
    public class CatalogCache<T>
    {
        public string CatalogFile { get; set; }
        public string[] Files { get; set; }
        public T[] Configs { get; set; }
        internal int ParsingCount;
        internal ConfigRequestBase Request;
    }
}
