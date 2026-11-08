using System;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.BattleResultUI
{
    public enum CombatRatingPromoteWayType { None, Skill, Equip }
    public sealed class CombatRatingPromoteWayItem : MonoBehaviour
    {
        public CombatRatingPromoteWayType Type;
        public Button GoToBtn;
        public WorldMultiLanguageText TipText;
        public Action<CombatRatingPromoteWayType> Navigate;
        private void Awake() => GoToBtn.onClick.AddListener(OnClickGoToBtn);
        private void OnEnable() => TipText.SetKey($"ui_game_end_promote_tip_{Type}");
        private void OnClickGoToBtn() => (Navigate ?? throw new InvalidOperationException(
            "Original combat promotion navigation is unbound."))(Type);
    }
}
