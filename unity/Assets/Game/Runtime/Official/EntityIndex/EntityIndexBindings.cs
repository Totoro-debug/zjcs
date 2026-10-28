using System.Collections.Generic;
namespace Zjcs.Official.EntityIndex
{
    public interface IEntityIndexContext
    {
        string LoadText(string originalPath);
        Dictionary<string, int[]> Decode(string context);
    }
}
