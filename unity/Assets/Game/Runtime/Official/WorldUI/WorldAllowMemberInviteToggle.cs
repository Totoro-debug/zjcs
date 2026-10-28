using System;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.WorldUI
{
    public interface IWorldMemberInviteServices
    {
        bool IsLeader { get; }
        bool PlayerSettingsAllowMemberInvite { get; set; }
        void OperPlayerAllowMemberInvite(bool allow, Action<bool> onSuccess);
    }

    // Original AllowMemberInviteToggle: field names, event lifetime and Toggle
    // notifications are retained, including the notification during RefreshAllow.
    public sealed class WorldAllowMemberInviteToggle : MonoBehaviour
    {
        [SerializeField] private GameObject mAllowMemberInviteObj;
        [SerializeField] private Toggle mAllowToggle;
        private const int kMinTeamAllowInvitePlayers = 3;
        private IWorldMemberInviteServices services;

        public void Initialize(IWorldMemberInviteServices value)
        { services = value ?? throw new ArgumentNullException(nameof(value)); }

        public void OnEnable()
        { mAllowToggle.onValueChanged.AddListener(HandleAllowValueChanged); }

        public void OnDisable()
        { mAllowToggle.onValueChanged.RemoveListener(HandleAllowValueChanged); }

        public void RefreshAllow(int teamMaxPlayers)
        {
            if (services.IsLeader && teamMaxPlayers >= kMinTeamAllowInvitePlayers)
            {
                mAllowToggle.isOn = services.PlayerSettingsAllowMemberInvite;
                mAllowMemberInviteObj.SetActive(true);
            }
            else mAllowMemberInviteObj.SetActive(false);
        }

        private void UpdateAllowMemberInviteToggle(bool allowMemberInvite)
        {
            if (mAllowToggle.isOn != allowMemberInvite)
                mAllowToggle.isOn = allowMemberInvite;
        }

        private void HandleAllowValueChanged(bool on)
        {
            if (services.IsLeader)
                services.OperPlayerAllowMemberInvite(on, result =>
                {
                    services.PlayerSettingsAllowMemberInvite = result;
                    UpdateAllowMemberInviteToggle(result);
                });
        }
    }
}
