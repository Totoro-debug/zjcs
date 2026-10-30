using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.TeamUI
{
    public sealed class TeamNpcIconDefinition { public string NameKey, Icon; }
    public interface ITeamNpcIconServices
    {
        int PlanesRank { get; }
        IProfessionPlayerInfoBindings PlayerInfo { get; }
        TeamNpcIconDefinition GetNpc(int classId);
        int GetProfession(int classId, int level, int planesRank);
        long CalcCombatRating(int classId, int level, int subRank);
    }

    // Original NpcIconUI. The team member's normal NPC branch still uses
    // PlayerIcon; this separate icon is used only by the original InitNpcIcon.
    public sealed class TeamNpcIconUI : UIBase, ITeamNpcIcon
    {
        public WorldMultiLanguageText NameText, CombatRatingText;
        public GameObject ProfessionIconGo;
        public Image ProfessionIcon, NpcIcon;
        public Button Button;
        private TeamNpcIconDefinition mInfo;
        private int npcId, npcLevel, npcSubRank, mProfession;
        private ITeamNpcIconServices services;
        private List<IDisposable> operations;
        private bool disposed;

        public void Configure(ITeamNpcIconServices value) => services = value;
        private void Awake() { if (Button != null) Button.onClick.AddListener(HandleOnClick); }
        protected override void OnEnableAndInited()
        {
            base.OnEnableAndInited();
            operations = null; disposed = false;
            Refresh();
        }
        protected override void OnDisableAndInited()
        {
            if (operations != null)
                for (int i = 0; i < operations.Count; i++) operations[i]?.Dispose();
            operations = null; disposed = true;
            base.OnDisableAndInited();
        }
        public void Init(int npcClassId, int level, int subRank)
        {
            InitWrapper(() =>
            {
                mInfo = services.GetNpc(npcClassId);
                mProfession = services.GetProfession(npcClassId, level, services.PlanesRank);
                npcId = npcClassId; npcLevel = level; npcSubRank = subRank;
            });
        }
        private Sprite Load(string path)
        {
            var operation = services.PlayerInfo.Resources.LoadAsset<Sprite>(path);
            if (disposed) operation.Dispose();
            else { if (operations == null) operations = new List<IDisposable>(); operations.Add(operation); }
            return operation.Result;
        }
        private void Refresh()
        {
            if (NameText != null) NameText.SetKey(mInfo.NameKey);
            Sprite icon = Load(mInfo.Icon);
            if (icon == null && npcId >= 11015 && npcId <= 11022)
                NpcIcon.gameObject.SetActive(false);
            else
            {
                NpcIcon.gameObject.SetActive(true);
                NpcIcon.sprite = icon;
            }
            if (CombatRatingText != null)
                CombatRatingText.Format(services.PlayerInfo.CompactNumber(services.CalcCombatRating(npcId, npcLevel, npcSubRank)));
            ProfessionIconGo.SetActive(mProfession != 0);
            if (mProfession != 0) ProfessionIcon.sprite = Load(services.PlayerInfo.FirstProfessionIcon(mProfession));
        }
        private void HandleOnClick() { }
    }
}
