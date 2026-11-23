using Zjcs.Official.HomeRelations;

public sealed class HomeRelationProbe : HomeMutex
{
    public int Changes, Restores;
    public override void OnChange() { Changes++; }
    public override void OnRestore() { Restores++; }
}
