using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Offline.Progression;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.World.NativeLabelUI.Framework;

namespace Zjcs.Offline.PartnersUI
{
    /// <summary>Selected profile -> original PlayerInfoData(Player) projection.</summary>
    public sealed class OfflinePartnerPlayerInfoSource
    {
        private readonly OfflineMainlineProgressSession session;
        private readonly OfflineCharacterProgressionService progression;
        private readonly OfflineProfessionAppearanceBindings appearance;
        private readonly Func<long> unixClock;
        private readonly Func<OfflinePlayerProgress, string> icon;
        private readonly PlayerTarget target;
        private Snapshot current;

        public IProfessionInfoPlayer MainPlayer => current;
        public SafeAction OnPlayerUpdate { get; } = new SafeAction();

        // Identity migration belongs to the profile host. No temporary or fixed
        // actor ID may enter the original portrait cache or dialogue history.
        public OfflinePartnerPlayerInfoSource(OfflineMainlineProgressSession session,
            OfflineCharacterProgressionService progression, OfflineProfessionAppearanceBindings appearance,
            Func<long> unixClock, Func<OfflinePlayerProgress, string> icon,
            Func<Sex, string> figure, Func<int, long, bool> canAssist)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.progression = progression ?? throw new ArgumentNullException(nameof(progression));
            this.appearance = appearance ?? throw new ArgumentNullException(nameof(appearance));
            this.unixClock = unixClock ?? throw new ArgumentNullException(nameof(unixClock));
            this.icon = icon ?? throw new ArgumentNullException(nameof(icon));
            target = new PlayerTarget(appearance.Id, figure, canAssist);
            SynchronizeCommittedPlayer(false);
        }

        // Local transactions replace Save.Player. Publish a complete replacement
        // at the original manager boundary; do not invent a server field-event order.
        public void SynchronizeCommittedPlayer(bool notify = true)
        {
            session.Reload();
            var player = progression.Player;
            if (!ReferenceEquals(player, session.Save.Player))
                throw new InvalidOperationException("Player progression belongs to a different local session.");
            if (!player.CharacterCreated || player.CharacterProperties.LocalActorIdentities.PlayerId != target.TargetId)
                throw new InvalidOperationException("A created selected player with its persisted actor ID is required.");
            long rating = progression.EvaluateProps(unixClock()).CombatRating;
            string iconPath = icon(player); // Original Player.Icon may be null.
            bool pinLimited = !progression.CanPinNpc;
            var collections = player.CharacterProperties.NpcFriendships.ToDictionary(value => value.NpcClassId, value => value.IsCollected);
            int petClass = player.Pets.Pets.FirstOrDefault(value => value.Index == player.Pets.FollowPetIndex)?.ClassId ?? 0;
            appearance.Refresh(AppearanceOperateSource.Normal, false);
            var data = new PlayerInfoData {
                TargetData = target, Icon = iconPath, Name = player.CharacterName,
                NameArg = new CultureTextArg(player.CharacterName, null, false),
                CombatRating = rating, SubRank = player.SubRank, Level = player.Level,
                AppearanceData = appearance.AppearanceData, Profession = appearance.Profession,
                Sex = appearance.Sex, EngagePetClassId = petClass
            };
            current = new Snapshot(this, data, pinLimited, collections);
            if (notify) OnPlayerUpdate.Invoke();
        }

        public PlayerInfoData BuildData(IProfessionInfoPlayer player)
        {
            if (!(player is Snapshot value) || !ReferenceEquals(value.Owner, this))
                throw new ArgumentException("Player info must belong to this selected profile source.", nameof(player));
            return value.Data;
        }

        private sealed class PlayerTarget : IProfessionInfoTarget
        {
            private readonly Func<Sex, string> figure;
            private readonly Func<int, long, bool> canAssist;
            internal PlayerTarget(long id, Func<Sex, string> figure, Func<int, long, bool> canAssist)
            {
                if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
                TargetId = id;
                this.figure = figure ?? throw new ArgumentNullException(nameof(figure));
                this.canAssist = canAssist ?? throw new ArgumentNullException(nameof(canAssist));
            }
            public ActorTargetType TargetType => ActorTargetType.Player;
            public long TargetId { get; }
            // ActorTargetData initializer, untouched by PlayerTargetData(long).
            public long ServerId => -1;
            public bool IsNpc => false;
            public string GetFigurePath(Sex sex) => figure(sex);
            public bool CheckActorCanAssist(int playType) => canAssist(playType, TargetId);
        }

        // Each committed generation is immutable. Original consumers can attach
        // their nine subscriptions; updates use OnPlayerUpdate and a new generation.
        private sealed class Snapshot : IProfessionInfoPlayer
        {
            internal readonly OfflinePartnerPlayerInfoSource Owner;
            internal readonly PlayerInfoData Data;
            private readonly Dictionary<int, bool> collections;
            internal Snapshot(OfflinePartnerPlayerInfoSource owner, PlayerInfoData data, bool limited, Dictionary<int, bool> collections)
            { Owner = owner; Data = data; IsNpcPinLimited = limited; this.collections = collections; }
            public long Id => Data.Id;
            public string Icon => Data.Icon;
            public string Name => Data.Name;
            public object AppearanceData => Data.AppearanceData;
            public int Profession => Data.Profession;
            public bool IsNpcPinLimited { get; }
            public bool? IsNpcCollected(int classId) => collections.TryGetValue(classId, out bool value) ? value : (bool?)null;
#pragma warning disable CS0067 // Immutable profile generation; original subscription contract.
            public event Action OnNameUpdate;
            public event Action<int> OnSubRankUpdate;
            public event Action<int> OnLevelUpdate;
            public event Action<long, long> OnCombatRatingChanged;
            public event Action<int, int> OnProfessionChange;
            public event Action<int> OnProfessionPromote;
            public event Action<AppearanceOperateSource> OnAppearanceUpdate;
            public event Action OnIconUpdate;
            public event Action<Sex> OnSexUpdate;
#pragma warning restore CS0067
        }
    }
}
