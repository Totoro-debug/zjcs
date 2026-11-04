using System;
using UnityEngine;

namespace Zjcs.Official.BattleUnitUI
{
    public interface IBattleHpSliderBindings
    {
        long Hp { get; }
        long LastMaxHp { get; }
        bool WatchesPlayer { get; }
        event Action<long> OnSpactatedPlayerId;
        event Action<long, long, long, long> OnHpOrShieldChange;
        void OnEnable();
        void OnDisable();
        GameEffectData GetData();
        GameObject Borrow(GameObject prefab, Transform slot, bool autoActive = true);
        void Return(GameObject effect, GameObject prefab);
    }
}
