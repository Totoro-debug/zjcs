using System;
using System.Collections.Generic;
using Zjcs.Official.ProfessionUI;
using PropType = Zjcs.Official.ProfessionUI.ProfessionPropertyType;

namespace Zjcs.Official.HomeHangUpWindowsUI
{
    // The original output component consumes only configuration and properties.
    // Keep the full window's existing binding while allowing its original subtree
    // to be hosted independently during the production HUD migration.
    public interface IHomeFurnitureOutputServices
    {
        IHomeWindowPropertyCenter PropertyCenter { get; }
        IReadOnlyDictionary<PropType, long> NonBattleProps { get; }
        HomeWindowHangUpInfo GetHangUpInfo(int id);
        string GetHangUpBaseOutput(int id);
        string FormatCount(long count);
        string TextFormatted(string key, params object[] args);
        double GetHangUpReward(int id, double hours, int level = -1, bool isBase = false);
        long GetHangUpMaxRewardStore(int id, int level = -1);
        double GetProp(IReadOnlyDictionary<PropType, long> props, PropType type);
        void GetHangUpProp(HomeWindowHangUpInfo info, IReadOnlyDictionary<PropType, long> props,
            out double addPercent, out double addFixed);
    }

    public sealed class HomeFurnitureOutputServices : IHomeFurnitureOutputServices
    {
        readonly IHomeHangUpWindowServices owner;
        public HomeFurnitureOutputServices(IHomeHangUpWindowServices owner)
        { this.owner = owner ?? throw new ArgumentNullException(nameof(owner)); }
        public IHomeWindowPropertyCenter PropertyCenter => owner.Player.PlayerPropCenter;
        public IReadOnlyDictionary<PropType, long> NonBattleProps => owner.Player.NonBattleProps;
        public HomeWindowHangUpInfo GetHangUpInfo(int id) => owner.GetHangUpInfo(id);
        public string GetHangUpBaseOutput(int id) => owner.GetHangUpBaseOutput(id);
        public string FormatCount(long count) => owner.FormatCount(count);
        public string TextFormatted(string key, params object[] args) => owner.TextFormatted(key, args);
        public double GetHangUpReward(int id, double hours, int level = -1, bool isBase = false)
            => owner.GetHangUpReward(id, hours, level, isBase);
        public long GetHangUpMaxRewardStore(int id, int level = -1) => owner.GetHangUpMaxRewardStore(id, level);
        public double GetProp(IReadOnlyDictionary<PropType, long> props, PropType type) => owner.GetProp(props, type);
        public void GetHangUpProp(HomeWindowHangUpInfo info, IReadOnlyDictionary<PropType, long> props,
            out double addPercent, out double addFixed) => owner.GetHangUpProp(info, props, out addPercent, out addFixed);
    }
}
