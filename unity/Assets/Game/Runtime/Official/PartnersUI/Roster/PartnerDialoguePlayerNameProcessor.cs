using System.Text.RegularExpressions;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.PartnersUI
{
    // Original PlayerNameTextProcessor and LanguageUtils replacement method.
    public sealed class PartnerDialoguePlayerNameProcessor : IWorldTextProcessor
    {
        private readonly string mPlayerName;
        public PartnerDialoguePlayerNameProcessor(string playerName) { mPlayerName = playerName; }
        public string Process(string text)
        {
            if (Regex.IsMatch(text, "<PN>")) return text.Replace("<PN>", mPlayerName);
            return text;
        }
    }
}
