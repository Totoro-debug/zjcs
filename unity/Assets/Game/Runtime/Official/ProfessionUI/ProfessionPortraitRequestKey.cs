namespace Zjcs.Official.ProfessionUI
{
    // The two consumed CharacterPortraitUtils methods, without its unrelated
    // reflection-based photographer construction and appearance hashing services.
    public static class ProfessionPortraitRequestKey
    {
        public static long GetLongHashCode(long uniqueId, int typeCode) => ((long)typeCode << 54) + uniqueId;
        public static int GetProfessionCode(int profession) => 50 + profession;
    }
}
