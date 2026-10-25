using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Zjcs.Presentation.Character
{
    public static class OfficialCharacterAnimationProfile
    {
        private static Dictionary<int, string> groups;

        public static string GroupForProfession(int profession)
        {
            if (profession < 0) throw new ArgumentOutOfRangeException(nameof(profession));
            if (groups == null) groups = LoadGroups();
            if (!groups.TryGetValue(profession, out string group))
                throw new InvalidDataException("Official character animation profile is missing profession " + profession);
            return group;
        }

        private static Dictionary<int, string> LoadGroups()
        {
            using (CharacterModelPackage package = CharacterModelPackage.OpenDefault())
            {
                Dictionary<string, object> profiles = Object(package.Catalog["profiles"], "character profiles");
                Dictionary<int, string> male = ReadGroups(Object(profiles["male"], "male character profiles"));
                Dictionary<int, string> female = ReadGroups(Object(profiles["female"], "female character profiles"));
                if (male.Count != female.Count)
                    throw new InvalidDataException("Official male and female animation profiles differ.");
                foreach (KeyValuePair<int, string> entry in male)
                    if (!female.TryGetValue(entry.Key, out string group) || group != entry.Value)
                        throw new InvalidDataException("Official male and female animation groups differ for profession "
                            + entry.Key);
                return male;
            }
        }

        private static Dictionary<int, string> ReadGroups(Dictionary<string, object> profiles)
        {
            var result = new Dictionary<int, string>();
            foreach (KeyValuePair<string, object> entry in profiles)
            {
                if (!int.TryParse(entry.Key, NumberStyles.Integer, CultureInfo.InvariantCulture,
                        out int profession))
                    throw new InvalidDataException("Official character animation profile has an invalid profession key.");
                Dictionary<string, object> profile = Object(entry.Value, "character profile " + profession);
                if (!profile.TryGetValue("pose", out object rawPose))
                    throw new InvalidDataException("Official character profile has no pose for profession " + profession);
                string pose = Convert.ToString(rawPose, CultureInfo.InvariantCulture);
                int separator = pose == null ? -1 : pose.IndexOf('/', StringComparison.Ordinal);
                if (separator <= 0)
                    throw new InvalidDataException("Official character profile pose has no animation group: " + pose);
                result.Add(profession, pose.Substring(0, separator));
            }
            return result;
        }

        private static Dictionary<string, object> Object(object value, string label)
        {
            if (value is Dictionary<string, object> result) return result;
            throw new InvalidDataException("Official " + label + " are malformed.");
        }
    }
}
