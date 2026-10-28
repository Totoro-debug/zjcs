namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System.Collections.Generic;
public sealed class ProfessionAppearanceBaseInfo
{
    public Profession Profession, PrePro;
    public Dictionary<Sex,Dictionary<SkinType,int>> DefaultSkins, AppendAppearanceRewards;
    public static readonly List<Profession> BaseProfessions = new List<Profession>
        { Profession.None, Profession.Zhanshi, Profession.Fashi };
}

}
