namespace Zjcs.Official.ProfessionUI
{
    public readonly struct AppearanceSkeletonPartEntry
    {
        public IAppearanceSkeletonPart Part { get; }
        public CharacterPartPoolEntry PoolEntry { get; }

        public AppearanceSkeletonPartEntry(IAppearanceSkeletonPart part, CharacterPartPoolEntry poolEntry)
        {
            Part = part;
            PoolEntry = poolEntry;
        }
    }
}
