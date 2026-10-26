using System.IO;

namespace Zjcs.Offline.State
{
    public static class ProfileSelectionPreference
    {
        private static string PathFor(string root) => Path.Combine(root, "profile-selection.txt");

        public static string Read(string root)
        {
            return LocalJsonSaveFile.Read(PathFor(root), ProfileSlotCatalog.TryParseSlotId).Json;
        }

        public static void Write(string root, string slot)
        {
            LocalJsonSaveFile.Write(PathFor(root), slot, ProfileSlotCatalog.TryParseSlotId);
        }
    }
}
