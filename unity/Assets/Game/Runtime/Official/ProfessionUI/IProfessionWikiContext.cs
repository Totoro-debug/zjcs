using System.Collections.Generic;
using System.Threading.Tasks;

namespace Zjcs.Official.ProfessionUI
{
    public sealed class ProfessionWikiInfo
    {
        public readonly string Type, URL;
        public readonly int Region, LimitConditionId;
        public ProfessionWikiInfo(string type, int region, int condition, string url)
        { Type = type; Region = region; LimitConditionId = condition; URL = url; }
    }
    public interface IProfessionWikiCondition { bool IsMet(IProfessionWikiPlayer player); }
    public interface IProfessionWikiPlayer
    {
        bool IsWikiLimited { get; }
        string GetShuShuAccountId();
        string Account { get; }
        int ServerId { get; }
        long Id { get; }
        string Name { get; }
        int Level { get; }
    }
    public interface IProfessionWikiContext
    {
        bool ServerWikiDisabled { get; }
        IProfessionWikiPlayer Player { get; }
        ProfessionWikiInfo GetWikiInfo(string type);
        string GetURL(string type);
        string GetTitleKey(string type);
        IProfessionWikiCondition GetCondition(int conditionId);
        string Localize(string key);
        void SystemMsg(string message);
        Task<string> OpenHtml(string url, string orientation, string navigation, string close);
        Dictionary<string, object> CreateEventData(string name, string accountId, string account);
        void ReportEvent(string name, Dictionary<string, object> fields);
        void PublishWikiClick();
    }
}
