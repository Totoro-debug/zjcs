using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Zjcs.Offline.State
{
    public sealed class ProfileSlotCatalog
    {
        private const string SavesDirectoryName = "saves";
        private const string ProgressFileName = "city-save.json";
        private const string DraftFileName = "character-draft.json";
        private const string CommerceFileName = "commerce-save.json";
        private const string CreationTimeFileName = "profile-created-at-unix-seconds";
        private static readonly object CreationLock = new object();
        private readonly string persistentRoot;

        public ProfileSlotCatalog(string persistentDataPath)
        {
            if (string.IsNullOrWhiteSpace(persistentDataPath))
                throw new ArgumentException("A persistent data path is required.", nameof(persistentDataPath));

            persistentRoot = Path.GetFullPath(persistentDataPath);
        }

        public IReadOnlyList<string> GetSlotIds()
        {
            string savesPath = Path.Combine(persistentRoot, SavesDirectoryName);
            if (!Directory.Exists(savesPath))
                return Array.Empty<string>();

            return Directory.EnumerateDirectories(savesPath, "*", SearchOption.TopDirectoryOnly)
                .Select(path => new DirectoryInfo(path))
                .Where(directory => (directory.Attributes & FileAttributes.ReparsePoint) == 0)
                .Select(directory => directory.Name)
                .Where(TryParseSlotId)
                .OrderBy(id => ParseSlotNumber(id))
                .ToArray();
        }

        public string CreateSlot()
        {
            lock (CreationLock)
            {
                string savesPath = Path.Combine(persistentRoot, SavesDirectoryName);
                Directory.CreateDirectory(savesPath);

                int next = GetSlotIds().Select(ParseSlotNumber).DefaultIfEmpty(0).Max() + 1;
                while (true)
                {
                    string id = "slot-" + next.ToString(CultureInfo.InvariantCulture);
                    string directory = GetSlotDirectory(id);
                    string reservation = Path.Combine(savesPath, "." + id + ".creating");
                    FileStream reservationStream;
                    try
                    {
                        reservationStream = new FileStream(reservation, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    }
                    catch (IOException) when (File.Exists(reservation))
                    {
                        next++;
                        continue;
                    }

                    try
                    {
                        using (reservationStream)
                        {
                            if (Directory.Exists(directory))
                            {
                                next++;
                                continue;
                            }

                            Directory.CreateDirectory(directory);
                            File.WriteAllText(Path.Combine(directory, CreationTimeFileName),
                                DateTimeOffset.Now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
                            return id;
                        }
                    }
                    finally
                    {
                        File.Delete(reservation);
                    }
                }
            }
        }

        public bool Contains(string slotId)
        {
            if (!TryParseSlotId(slotId))
                return false;

            string directory = GetSlotDirectory(slotId);
            return Directory.Exists(directory)
                && (new DirectoryInfo(directory).Attributes & FileAttributes.ReparsePoint) == 0;
        }

        public string GetSlotDirectory(string slotId)
        {
            if (!TryParseSlotId(slotId))
                throw new ArgumentException("Invalid profile slot id.", nameof(slotId));

            return Path.Combine(persistentRoot, SavesDirectoryName, slotId);
        }

        public string GetProgressPath(string slotId)
        {
            return Path.Combine(GetSlotDirectory(slotId), ProgressFileName);
        }

        public string GetProgressBackupPath(string slotId)
        {
            return GetProgressPath(slotId) + ".bak";
        }

        public string GetDraftPath(string slotId)
        {
            return Path.Combine(GetSlotDirectory(slotId), DraftFileName);
        }

        public string GetCommercePath(string slotId)
        {
            return Path.Combine(GetSlotDirectory(slotId), CommerceFileName);
        }

        public long GetCreatedAtUnixSeconds(string slotId)
        {
            string path = Path.Combine(GetSlotDirectory(slotId), CreationTimeFileName);
            if (!File.Exists(path)) return 0;
            string value = File.ReadAllText(path).Trim();
            return long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long result)
                && result > 0 ? result : 0;
        }

        public static bool TryParseSlotId(string slotId)
        {
            if (string.IsNullOrEmpty(slotId) || !slotId.StartsWith("slot-", StringComparison.Ordinal))
                return false;

            string number = slotId.Substring("slot-".Length);
            return int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out int value)
                && value > 0
                && string.Equals(value.ToString(CultureInfo.InvariantCulture), number, StringComparison.Ordinal);
        }

        private static int ParseSlotNumber(string slotId)
        {
            return int.Parse(slotId.Substring("slot-".Length), NumberStyles.None, CultureInfo.InvariantCulture);
        }
    }
}
