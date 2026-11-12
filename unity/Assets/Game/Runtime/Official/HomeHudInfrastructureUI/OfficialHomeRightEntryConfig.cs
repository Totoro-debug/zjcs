using System;
using System.Linq;
using UnityEngine;
namespace Zjcs.Official.HomeHudInfrastructureUI
{
    public sealed class OfficialHomeRightEntryConfig
    {
        [Serializable] sealed class Payload { public string manifest;public int checkConditionId;public Row[] entries; }
        [Serializable] sealed class Row { public int id,sortedId,activityType,activityId;public string entryPath;public bool isActivity,hasActivityType,hasActivityId; }
        public int CheckConditionId { get; }
        public HomeRightEntryUIActivityInfoParser Entries { get; }
        OfficialHomeRightEntryConfig(Payload data)
        {
            CheckConditionId=data.checkConditionId;
            Entries=new HomeRightEntryUIActivityInfoParser(data.entries.Select(r=>new HomeRightEntryUIActivityInfo(r.id,r.sortedId,r.entryPath,r.isActivity,
                r.hasActivityType?(ActivityType?)r.activityType:null,r.hasActivityId?(int?)r.activityId:null)).ToArray());
        }
        public static OfficialHomeRightEntryConfig LoadDefault()
        {
            var text=Resources.Load<TextAsset>("Home/HomeRightEntryConfig");if(!text)throw new InvalidOperationException("Original home right-entry configuration is unbound.");
            var data=JsonUtility.FromJson<Payload>(text.text);if(data==null||data.manifest!="884_156692"||data.entries==null)throw new InvalidOperationException("Unexpected original home right-entry configuration.");
            return new OfficialHomeRightEntryConfig(data);
        }
    }
}
