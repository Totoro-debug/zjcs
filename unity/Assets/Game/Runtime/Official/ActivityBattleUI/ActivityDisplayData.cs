using System;

namespace Zjcs.Official.ActivityBattleUI
{
    // The activity adapter supplies the original display inputs. This value
    // does not choose activity periods, rewards or preview progress.
    public sealed class ActivityDisplayData
    {
        public string NameKey { get; }
        public DateTime EndDate { get; }
        public ActivityDisplayData(string nameKey, DateTime endDate)
        {
            NameKey = nameKey ?? throw new ArgumentNullException(nameof(nameKey));
            EndDate = endDate;
        }
    }
}
