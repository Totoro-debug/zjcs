using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Zjcs.Offline.State;
using UnityEngine;

namespace Zjcs.Offline.Home
{
    [Serializable]
    public sealed class OfflineHomeSave
    {
        public int Schema = 2;
        public long LastLocalUnixSeconds;
        public long RandomState;
        public List<OfflineHomeIncomeEntry> Income = new List<OfflineHomeIncomeEntry>();
        public List<OfflineHomeItemStack> Inventory = new List<OfflineHomeItemStack>();
        public List<OfflineHomeRandomRewardSlot> RandomRewards = new List<OfflineHomeRandomRewardSlot>();
        public List<OfflineHomeFastForwardUsage> FastForwardUsage = new List<OfflineHomeFastForwardUsage>();
        public List<OfflineHomeMail> Mail = new List<OfflineHomeMail>();
        public OfflineHomeTutorialProgress Tutorial = new OfflineHomeTutorialProgress();
        public List<OfflineHomeFurnitureEntry> Furniture = new List<OfflineHomeFurnitureEntry>();
        public List<OfflineHomeHangUpEntry> HangUps = new List<OfflineHomeHangUpEntry>();
        public List<OfflineHomeSkinSelection> Skins = new List<OfflineHomeSkinSelection>();
        public long GrantSerial;
        public List<string> AppliedReceipts = new List<string>();
    }

    [Serializable]
    public sealed class OfflineHomeFurnitureEntry
    {
        public int Type;
        public bool Owned;
    }

    [Serializable]
    public sealed class OfflineHomeHangUpEntry
    {
        public int HangUpId;
        public int Level = 1;
        public bool Unlocked;
    }

    [Serializable]
    public sealed class OfflineHomeSkinSelection
    {
        public int Category;
        public int SkinItemId;
    }

    [Serializable]
    public sealed class OfflineHomeIncomeEntry
    {
        public int HangUpId;
        public double StoredSeconds;
        public long SettledCount;
    }

    [Serializable]
    public sealed class OfflineHomeItemStack
    {
        public int ItemId;
        public long Count;
        public long Serial;
    }

    [Serializable]
    public sealed class OfflineHomeRandomRewardSlot
    {
        public int Slot;
        public int ItemId;
        public long Count;
        public long NextRefreshUnixSeconds;
    }

    [Serializable]
    public sealed class OfflineHomeFastForwardUsage
    {
        public int FastHangUpId;
        public string LocalDate;
        public int FreeUses;
    }

    [Serializable]
    public sealed class OfflineHomeMail
    {
        public long Id;
        public long ExpireUnixSeconds;
        public int Status;
        public string Sender;
        public string Subject;
        public string Body;
        public List<OfflineHomeItemStack> Attachments = new List<OfflineHomeItemStack>();
    }

    [Serializable]
    public sealed class OfflineHomeTutorialProgress
    {
        public string GuideId;
        public int Step;
        public List<string> PendingGuideIds = new List<string>();
    }

    [Serializable]
    public sealed class OfflineHomeRewardChoice
    {
        public int ItemId;
        public long Count;
        public int Weight;
    }

    [Serializable]
    public sealed class OfflineHomeRandomRewardDefinition
    {
        public int Slot;
        public int Type;
        public long MinimumIntervalSeconds;
        public long MaximumIntervalSeconds;
        public int MaximumCount;
        public bool Unlocked;
        public List<OfflineHomeRewardChoice> Rewards = new List<OfflineHomeRewardChoice>();
    }

    [Serializable]
    public sealed class OfflineHomeIncomeDefinition
    {
        public int HangUpId;
        public int OutputItemId;
        public int[] OutputItemIds;
        public int[] OutputWeights;
        public int FastHangUpId;
        public int FreeFastForwardLimit;
        public double FreeFastForwardHours;
        public HangUpIncomeRule Rule = new HangUpIncomeRule();
        public bool Unlocked;
    }

    public sealed class OfflineHomeIncomeSession
    {
        private const int ReceiptLimit = 1000000;
        private readonly string savePath;
        private OfflineHomeIncomeDefinition[] definitions;
        private IReadOnlyDictionary<int, long> properties;
        private ISet<int> floatPropertyIds;
        private OfflineHomeRandomRewardDefinition[] randomRewardDefinitions;

        private OfflineHomeIncomeSession(string savePath, OfflineHomeIncomeDefinition[] definitions,
            IReadOnlyDictionary<int, long> properties, ISet<int> floatPropertyIds,
            OfflineHomeRandomRewardDefinition[] randomRewardDefinitions, OfflineHomeSave save,
            SaveReadStatus readStatus)
        {
            this.savePath = savePath;
            this.definitions = definitions;
            this.properties = properties;
            this.floatPropertyIds = floatPropertyIds;
            this.randomRewardDefinitions = randomRewardDefinitions;
            Save = save;
            ReadStatus = readStatus;
        }

        public OfflineHomeSave Save { get; private set; }
        public SaveReadStatus ReadStatus { get; }
        public IReadOnlyList<OfflineHomeIncomeDefinition> Definitions => definitions;

        public static OfflineHomeIncomeSession Open(string savePath,
            IEnumerable<OfflineHomeIncomeDefinition> definitions,
            IReadOnlyDictionary<int, long> properties, ISet<int> floatPropertyIds, long nowUnixSeconds)
        {
            return Open(savePath, definitions, properties, floatPropertyIds,
                Array.Empty<OfflineHomeRandomRewardDefinition>(), nowUnixSeconds);
        }

        public static OfflineHomeIncomeSession Open(ProfileSlotCatalog slots, string slotId,
            IEnumerable<OfflineHomeIncomeDefinition> definitions,
            IReadOnlyDictionary<int, long> properties, ISet<int> floatPropertyIds, long nowUnixSeconds)
        {
            if (slots == null) throw new ArgumentNullException(nameof(slots));
            if (!slots.Contains(slotId)) throw new DirectoryNotFoundException("The selected profile slot does not exist.");
            return Open(slots.GetHomePath(slotId), definitions, properties, floatPropertyIds, nowUnixSeconds);
        }

        public static OfflineHomeIncomeSession Open(string savePath,
            IEnumerable<OfflineHomeIncomeDefinition> definitions,
            IReadOnlyDictionary<int, long> properties, ISet<int> floatPropertyIds,
            IEnumerable<OfflineHomeRandomRewardDefinition> randomRewards, long nowUnixSeconds)
        {
            if (string.IsNullOrWhiteSpace(savePath)) throw new ArgumentException("A save path is required.", nameof(savePath));
            OfflineHomeIncomeDefinition[] catalog = (definitions ?? throw new ArgumentNullException(nameof(definitions))).ToArray();
            ValidateCatalog(catalog);
            OfflineHomeRandomRewardDefinition[] rewardCatalog = (randomRewards ?? throw new ArgumentNullException(nameof(randomRewards))).ToArray();
            ValidateRewardCatalog(rewardCatalog);
            SaveReadResult result = LocalJsonSaveFile.Read(savePath, IsValidJson);
            if (result.Status == SaveReadStatus.Corrupt)
                throw new InvalidDataException("The selected home save is corrupt.");
            OfflineHomeSave save = result.Json == null ? new OfflineHomeSave() : JsonUtility.FromJson<OfflineHomeSave>(result.Json);
            UpgradeSave(save);
            if (result.Json == null) save.LastLocalUnixSeconds = Math.Max(0, nowUnixSeconds);
            var session = new OfflineHomeIncomeSession(savePath, catalog, properties, floatPropertyIds,
                rewardCatalog, save, result.Status);
            session.AdvanceTo(nowUnixSeconds);
            return session;
        }

        public static OfflineHomeIncomeSession Open(ProfileSlotCatalog slots, string slotId,
            IEnumerable<OfflineHomeIncomeDefinition> definitions,
            IReadOnlyDictionary<int, long> properties, ISet<int> floatPropertyIds,
            IEnumerable<OfflineHomeRandomRewardDefinition> randomRewards, long nowUnixSeconds)
        {
            if (slots == null) throw new ArgumentNullException(nameof(slots));
            if (!slots.Contains(slotId)) throw new DirectoryNotFoundException("The selected profile slot does not exist.");
            return Open(slots.GetHomePath(slotId), definitions, properties, floatPropertyIds,
                randomRewards, nowUnixSeconds);
        }

        public HangUpIncomePreview Preview(int hangUpId)
        {
            OfflineHomeIncomeDefinition definition = FindDefinition(hangUpId);
            OfflineHomeIncomeEntry entry = FindEntry(Save, hangUpId);
            return OfflineHangUpIncome.Preview(ToRecord(entry), definition.Rule, properties, floatPropertyIds);
        }

        public bool AdvanceTo(long nowUnixSeconds)
        {
            OfflineHomeSave next = Clone(Save);
            long previousCheckpoint = next.LastLocalUnixSeconds;
            long elapsed = OfflineHangUpIncome.AdvanceCheckpoint(ref next.LastLocalUnixSeconds, nowUnixSeconds);
            if (elapsed > 0)
            {
                foreach (OfflineHomeIncomeDefinition definition in definitions)
                {
                    if (!definition.Unlocked) continue;
                    OfflineHomeIncomeEntry entry = FindOrCreate(next, definition.HangUpId);
                    double capacitySeconds = Math.Max(0, definition.Rule.MaxStoreHours) * 3600.0;
                    entry.StoredSeconds = Math.Min(capacitySeconds,
                        Math.Max(0, entry.StoredSeconds) + elapsed);
                }
            }
            AdvanceRandomRewards(next, next.LastLocalUnixSeconds);
            if (elapsed == 0 && previousCheckpoint == next.LastLocalUnixSeconds
                && JsonUtility.ToJson(next) == JsonUtility.ToJson(Save)) return false;
            return Commit(next);
        }

        public IReadOnlyList<OfflineHomeRandomRewardSlot> RandomRewards => Save.RandomRewards;

        public bool CollectRandomReward(int slot, string receipt)
        {
            if (string.IsNullOrWhiteSpace(receipt) || Save.AppliedReceipts.Contains(receipt)) return false;
            OfflineHomeRandomRewardSlot reward = Save.RandomRewards.FirstOrDefault(value => value.Slot == slot);
            OfflineHomeRandomRewardDefinition definition = randomRewardDefinitions.FirstOrDefault(value => value.Slot == slot);
            if (reward == null || definition == null || !definition.Unlocked || reward.ItemId <= 0 || reward.Count <= 0)
                return false;
            OfflineHomeSave next = Clone(Save);
            OfflineHomeRandomRewardSlot stored = next.RandomRewards.First(value => value.Slot == slot);
            if (!Grant(next, stored.ItemId, stored.Count)) return false;
            stored.ItemId = 0;
            stored.Count = 0;
            stored.NextRefreshUnixSeconds = AddSaturated(Save.LastLocalUnixSeconds,
                RandomInterval(definition, next));
            AddReceipt(next, receipt);
            return Commit(next);
        }

        public IReadOnlyList<OfflineHomeMail> GetInbox(long nowUnixSeconds)
        {
            return Save.Mail.Where(value => value.ExpireUnixSeconds <= 0 || value.ExpireUnixSeconds > nowUnixSeconds)
                .OrderBy(value => value.Status == 0 ? 0 : value.Status == 1 ? 1 : 2)
                .ThenByDescending(value => value.Id).ToArray();
        }

        public bool MarkMailRead(long mailId)
        {
            OfflineHomeMail current = Save.Mail.FirstOrDefault(value => value.Id == mailId);
            if (current == null || IsExpired(current, Save.LastLocalUnixSeconds)) return false;
            return UpdateMail(mailId, mail =>
            {
                if (mail.Status == 0) mail.Status = 1;
            });
        }

        public bool AddLocalMail(OfflineHomeMail mail)
        {
            if (!IsValidMail(mail) || Save.Mail.Any(value => value.Id == mail.Id)) return false;
            OfflineHomeSave next = Clone(Save);
            next.Mail.Add(CloneMail(mail));
            return Commit(next);
        }

        public int MarkAllMailRead(long nowUnixSeconds)
        {
            OfflineHomeSave next = Clone(Save);
            int changed = 0;
            foreach (OfflineHomeMail mail in next.Mail)
            {
                if (mail.ExpireUnixSeconds > 0 && mail.ExpireUnixSeconds <= nowUnixSeconds) continue;
                if (mail.Status != 0) continue;
                mail.Status = 1;
                changed++;
            }
            return changed > 0 && Commit(next) ? changed : 0;
        }

        public bool CollectMailAttachments(long mailId, string receipt)
        {
            OfflineHomeMail mail = Save.Mail.FirstOrDefault(value => value.Id == mailId);
            if (mail == null || IsExpired(mail, Save.LastLocalUnixSeconds)) return false;
            return CollectMailAttachments(mailId, Enumerable.Range(0, mail.Attachments.Count), receipt);
        }

        public bool CollectMailAttachments(long mailId, IEnumerable<int> attachmentIndices, string receipt)
        {
            if (string.IsNullOrWhiteSpace(receipt) || Save.AppliedReceipts.Contains(receipt)) return false;
            OfflineHomeMail mail = Save.Mail.FirstOrDefault(value => value.Id == mailId);
            if (mail == null || IsExpired(mail, Save.LastLocalUnixSeconds)
                || mail.Status >= 2 || mail.Attachments.Count == 0) return false;
            int[] indices = (attachmentIndices ?? Enumerable.Empty<int>()).Distinct().OrderByDescending(value => value).ToArray();
            if (indices.Length == 0 || indices.Any(value => value < 0 || value >= mail.Attachments.Count)) return false;
            OfflineHomeSave next = Clone(Save);
            OfflineHomeMail stored = next.Mail.First(value => value.Id == mailId);
            foreach (int index in indices)
            {
                OfflineHomeItemStack item = stored.Attachments[index];
                if (!Grant(next, item.ItemId, item.Count)) return false;
                stored.Attachments.RemoveAt(index);
            }
            stored.Status = stored.Attachments.Count == 0 ? 2 : 1;
            AddReceipt(next, receipt);
            return Commit(next);
        }

        public int CollectAllMailAttachments(long nowUnixSeconds, string receipt)
        {
            if (string.IsNullOrWhiteSpace(receipt) || Save.AppliedReceipts.Contains(receipt)) return 0;
            OfflineHomeSave next = Clone(Save);
            int collected = 0;
            foreach (OfflineHomeMail mail in next.Mail)
            {
                if (mail.ExpireUnixSeconds > 0 && mail.ExpireUnixSeconds <= nowUnixSeconds
                    || mail.Status >= 2 || mail.Attachments.Count == 0) continue;
                foreach (OfflineHomeItemStack item in mail.Attachments)
                    if (!Grant(next, item.ItemId, item.Count)) return 0;
                mail.Attachments.Clear();
                mail.Status = 2;
                collected++;
            }
            if (collected == 0) return 0;
            AddReceipt(next, receipt);
            return Commit(next) ? collected : 0;
        }

        public bool DeleteMail(long mailId)
        {
            OfflineHomeSave next = Clone(Save);
            OfflineHomeMail mail = next.Mail.FirstOrDefault(value => value.Id == mailId);
            if (mail == null) return false;
            next.Mail.Remove(mail);
            return Commit(next);
        }

        public bool DeleteReadMail()
        {
            OfflineHomeSave next = Clone(Save);
            int removed = next.Mail.RemoveAll(value => value.Status >= 2 || (value.Status >= 1 && value.Attachments.Count == 0));
            return removed > 0 && Commit(next);
        }

        public bool SetTutorialProgress(string guideId, int step, IEnumerable<string> pendingGuideIds)
        {
            if (string.IsNullOrWhiteSpace(guideId) || step < 0) return false;
            OfflineHomeSave next = Clone(Save);
            next.Tutorial.GuideId = guideId;
            next.Tutorial.Step = step;
            next.Tutorial.PendingGuideIds = (pendingGuideIds ?? Enumerable.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value)).Distinct().ToList();
            return Commit(next);
        }

        /// <summary>
        /// Clone, mutate, validate and commit the home save in one transaction.
        /// A repeated receipt is rejected before the mutation runs.
        /// </summary>
        public bool Mutate(string receipt, Action<OfflineHomeSave> mutation)
        {
            if (mutation == null) throw new ArgumentNullException(nameof(mutation));
            if (string.IsNullOrWhiteSpace(receipt)) throw new ArgumentException("A receipt is required.", nameof(receipt));
            if (Save.AppliedReceipts.Contains(receipt)) return false;
            OfflineHomeSave next = Clone(Save);
            mutation(next);
            AddReceipt(next, receipt);
            return Commit(next);
        }

        public IReadOnlyList<OfflineHomeFurnitureEntry> Furniture => Save.Furniture;

        public IReadOnlyList<OfflineHomeHangUpEntry> HangUps => Save.HangUps;

        public IReadOnlyList<OfflineHomeSkinSelection> Skins => Save.Skins;

        public bool OwnsFurniture(int type)
        {
            OfflineHomeFurnitureEntry entry = Save.Furniture.FirstOrDefault(value => value.Type == type);
            return entry != null && entry.Owned;
        }

        public OfflineHomeHangUpEntry HangUpState(int hangUpId)
        {
            return Save.HangUps.FirstOrDefault(value => value.HangUpId == hangUpId);
        }

        public int HangUpLevel(int hangUpId, int fallback = 1)
        {
            OfflineHomeHangUpEntry entry = HangUpState(hangUpId);
            return entry == null ? fallback : Math.Max(1, entry.Level);
        }

        public bool IsHangUpUnlocked(int hangUpId)
        {
            OfflineHomeHangUpEntry entry = HangUpState(hangUpId);
            return entry != null && entry.Unlocked;
        }

        public OfflineHomeSkinSelection SkinOf(int category)
        {
            return Save.Skins.FirstOrDefault(value => value.Category == category);
        }

        /// <summary>Removes a drained ledger stack once the player inventory holds it.</summary>
        public bool AcknowledgeInventory(long serial, string receipt)
        {
            if (serial <= 0 || string.IsNullOrWhiteSpace(receipt) || Save.AppliedReceipts.Contains(receipt)) return false;
            OfflineHomeSave next = Clone(Save);
            OfflineHomeItemStack stack = next.Inventory.FirstOrDefault(value => value.Serial == serial);
            if (stack == null) return false;
            next.Inventory.Remove(stack);
            AddReceipt(next, receipt);
            return Commit(next);
        }

        /// <summary>Rebuilds the income catalog after a level or property change.</summary>
        public void Reconfigure(IEnumerable<OfflineHomeIncomeDefinition> catalog,
            IReadOnlyDictionary<int, long> playerProperties, ISet<int> floatPropertyIds,
            IEnumerable<OfflineHomeRandomRewardDefinition> randomRewards)
        {
            OfflineHomeIncomeDefinition[] definitionsNext = (catalog ?? throw new ArgumentNullException(nameof(catalog))).ToArray();
            ValidateCatalog(definitionsNext);
            OfflineHomeRandomRewardDefinition[] rewardsNext = (randomRewards ?? throw new ArgumentNullException(nameof(randomRewards))).ToArray();
            ValidateRewardCatalog(rewardsNext);
            definitions = definitionsNext;
            randomRewardDefinitions = rewardsNext;
            properties = playerProperties ?? new Dictionary<int, long>();
            this.floatPropertyIds = floatPropertyIds ?? new HashSet<int>();
        }

        public long Collect(int hangUpId, string receipt)
        {
            if (string.IsNullOrWhiteSpace(receipt) || Save.AppliedReceipts.Contains(receipt)) return 0;
            OfflineHomeIncomeDefinition definition = FindDefinition(hangUpId);
            if (!definition.Unlocked) return 0;
            OfflineHomeSave next = Clone(Save);
            OfflineHomeIncomeEntry entry = FindOrCreate(next, hangUpId);
            HangUpIncomeRecord record = ToRecord(entry);
            long count = OfflineHangUpIncome.Collect(record, definition.Rule, properties, floatPropertyIds);
            if (count <= 0) return 0;
            entry.StoredSeconds = record.StoredSeconds;
            entry.SettledCount = record.SettledCount;
            if (!GrantOutput(next, definition, count)) return 0;
            AddReceipt(next, receipt);
            return Commit(next) ? count : 0;
        }

        public long ApplyFastForward(int hangUpId, double hours, string receipt)
        {
            if (!IsFinite(hours) || hours <= 0 || string.IsNullOrWhiteSpace(receipt)
                || Save.AppliedReceipts.Contains(receipt)) return 0;
            OfflineHomeIncomeDefinition definition = FindDefinition(hangUpId);
            if (!definition.Unlocked || definition.FastHangUpId <= 0
                || definition.FreeFastForwardLimit <= 0 || hours != definition.FreeFastForwardHours) return 0;
            string localDate = LocalDate(Save.LastLocalUnixSeconds);
            OfflineHomeFastForwardUsage usage = Save.FastForwardUsage.FirstOrDefault(value => value.FastHangUpId == definition.FastHangUpId);
            int used = usage != null && usage.LocalDate == localDate ? usage.FreeUses : 0;
            if (used >= definition.FreeFastForwardLimit) return 0;
            long count = WholeCount(OfflineHangUpIncome.HourlyRate(definition.Rule, properties, floatPropertyIds) * hours);
            if (count <= 0) return 0;
            OfflineHomeSave next = Clone(Save);
            OfflineHomeFastForwardUsage storedUsage = next.FastForwardUsage.FirstOrDefault(value => value.FastHangUpId == definition.FastHangUpId);
            if (storedUsage == null)
            {
                storedUsage = new OfflineHomeFastForwardUsage { FastHangUpId = definition.FastHangUpId };
                next.FastForwardUsage.Add(storedUsage);
            }
            storedUsage.LocalDate = localDate;
            storedUsage.FreeUses = used + 1;
            if (!GrantOutput(next, definition, count)) return 0;
            AddReceipt(next, receipt);
            return Commit(next) ? count : 0;
        }

        public long ApplyFastForwardItems(int hangUpId, int timeCrystalItemId, int itemCount,
            int configuredMaximumItemCount, double hoursPerItem, string receipt)
        {
            if (timeCrystalItemId <= 0 || itemCount <= 0 || configuredMaximumItemCount <= 0
                || itemCount > Math.Min(100, configuredMaximumItemCount)
                || !IsFinite(hoursPerItem) || hoursPerItem <= 0
                || string.IsNullOrWhiteSpace(receipt) || Save.AppliedReceipts.Contains(receipt)) return 0;
            OfflineHomeIncomeDefinition definition = FindDefinition(hangUpId);
            if (!definition.Unlocked) return 0;
            double totalHours = itemCount * hoursPerItem;
            long count = WholeCount(OfflineHangUpIncome.HourlyRate(definition.Rule, properties, floatPropertyIds) * totalHours);
            if (count <= 0) return 0;
            OfflineHomeSave next = Clone(Save);
            if (!Consume(next, timeCrystalItemId, itemCount)
                || !GrantOutput(next, definition, count)) return 0;
            AddReceipt(next, receipt);
            return Commit(next) ? count : 0;
        }

        private bool Commit(OfflineHomeSave next)
        {
            if (!IsValid(next)) return false;
            string json = JsonUtility.ToJson(next);
            LocalJsonSaveFile.Write(savePath, json, IsValidJson);
            Save = next;
            return true;
        }

        private bool UpdateMail(long mailId, Action<OfflineHomeMail> update)
        {
            OfflineHomeSave next = Clone(Save);
            OfflineHomeMail mail = next.Mail.FirstOrDefault(value => value.Id == mailId);
            if (mail == null) return false;
            update(mail);
            return Commit(next);
        }

        private void AdvanceRandomRewards(OfflineHomeSave save, long nowUnixSeconds)
        {
            foreach (OfflineHomeRandomRewardDefinition definition in randomRewardDefinitions)
            {
                if (!definition.Unlocked) continue;
                int activeOfType = save.RandomRewards.Count(value => value.ItemId > 0
                    && randomRewardDefinitions.Any(candidate => candidate.Slot == value.Slot && candidate.Type == definition.Type));
                if (activeOfType >= definition.MaximumCount) continue;
                OfflineHomeRandomRewardSlot slot = save.RandomRewards.FirstOrDefault(value => value.Slot == definition.Slot);
                if (slot != null && slot.ItemId > 0) continue;
                long due = slot?.NextRefreshUnixSeconds ?? nowUnixSeconds;
                if (due > nowUnixSeconds) continue;
                OfflineHomeRewardChoice choice = ChooseReward(definition, save);
                if (choice == null) continue;
                if (slot == null)
                {
                    slot = new OfflineHomeRandomRewardSlot { Slot = definition.Slot };
                    save.RandomRewards.Add(slot);
                }
                slot.ItemId = choice.ItemId;
                slot.Count = choice.Count;
                slot.NextRefreshUnixSeconds = AddSaturated(nowUnixSeconds,
                    RandomInterval(definition, save));
            }
        }

        private static OfflineHomeRewardChoice ChooseReward(OfflineHomeRandomRewardDefinition definition, OfflineHomeSave save)
        {
            int total = definition.Rewards.Sum(value => value.Weight);
            if (total <= 0) return null;
            ulong state = (ulong)save.RandomState;
            if (state == 0) state = 0x9e3779b97f4a7c15UL;
            state ^= state << 13;
            state ^= state >> 7;
            state ^= state << 17;
            save.RandomState = (long)state;
            int roll = (int)(state % (uint)total);
            foreach (OfflineHomeRewardChoice reward in definition.Rewards)
            {
                roll -= reward.Weight;
                if (roll < 0) return reward;
            }
            return definition.Rewards[definition.Rewards.Count - 1];
        }

        private static long RandomInterval(OfflineHomeRandomRewardDefinition definition, OfflineHomeSave save)
        {
            long span = definition.MaximumIntervalSeconds - definition.MinimumIntervalSeconds;
            if (span <= 0) return definition.MinimumIntervalSeconds;
            ulong state = (ulong)save.RandomState;
            state = state * 6364136223846793005UL + 1442695040888963407UL;
            save.RandomState = (long)state;
            return definition.MinimumIntervalSeconds + (long)(state % (ulong)(span + 1));
        }

        private static long AddSaturated(long left, long right)
        {
            return left > long.MaxValue - right ? long.MaxValue : left + right;
        }

        private static void UpgradeSave(OfflineHomeSave save)
        {
            if (save.Schema == 1)
            {
                save.Schema = 2;
                save.RandomRewards = new List<OfflineHomeRandomRewardSlot>();
                save.Mail = new List<OfflineHomeMail>();
                save.Tutorial = new OfflineHomeTutorialProgress();
            }
            if (save.RandomRewards == null) save.RandomRewards = new List<OfflineHomeRandomRewardSlot>();
            if (save.FastForwardUsage == null) save.FastForwardUsage = new List<OfflineHomeFastForwardUsage>();
            if (save.Mail == null) save.Mail = new List<OfflineHomeMail>();
            if (save.Tutorial == null) save.Tutorial = new OfflineHomeTutorialProgress();
            if (save.Tutorial.PendingGuideIds == null) save.Tutorial.PendingGuideIds = new List<string>();
            if (save.Inventory == null) save.Inventory = new List<OfflineHomeItemStack>();
            if (save.Income == null) save.Income = new List<OfflineHomeIncomeEntry>();
            if (save.Furniture == null) save.Furniture = new List<OfflineHomeFurnitureEntry>();
            if (save.HangUps == null) save.HangUps = new List<OfflineHomeHangUpEntry>();
            if (save.Skins == null) save.Skins = new List<OfflineHomeSkinSelection>();
            if (save.GrantSerial < 0) save.GrantSerial = 0;
            foreach (OfflineHomeItemStack stack in save.Inventory)
                if (stack != null && stack.Serial <= 0) stack.Serial = ++save.GrantSerial;
            if (save.AppliedReceipts == null) save.AppliedReceipts = new List<string>();
        }

        private OfflineHomeIncomeDefinition FindDefinition(int id)
        {
            OfflineHomeIncomeDefinition result = definitions.FirstOrDefault(value => value.HangUpId == id);
            if (result == null) throw new ArgumentOutOfRangeException(nameof(id), "Hang-up ID is not in the official catalog.");
            return result;
        }

        private static OfflineHomeIncomeEntry FindEntry(OfflineHomeSave save, int id)
        {
            return save.Income.FirstOrDefault(value => value.HangUpId == id)
                ?? new OfflineHomeIncomeEntry { HangUpId = id };
        }

        private static OfflineHomeIncomeEntry FindOrCreate(OfflineHomeSave save, int id)
        {
            OfflineHomeIncomeEntry entry = save.Income.FirstOrDefault(value => value.HangUpId == id);
            if (entry != null) return entry;
            entry = new OfflineHomeIncomeEntry { HangUpId = id };
            save.Income.Add(entry);
            return entry;
        }

        private static HangUpIncomeRecord ToRecord(OfflineHomeIncomeEntry entry)
        {
            return new HangUpIncomeRecord { StoredSeconds = entry.StoredSeconds, SettledCount = entry.SettledCount };
        }

        private static bool GrantOutput(OfflineHomeSave save, OfflineHomeIncomeDefinition definition, long count)
        {
            int itemId = definition.OutputItemId;
            if (definition.OutputItemIds != null && definition.OutputItemIds.Length > 1)
            {
                int total = 0;
                foreach (int weight in definition.OutputWeights ?? Array.Empty<int>()) total += Math.Max(0, weight);
                if (total > 0)
                {
                    ulong state = (ulong)save.RandomState;
                    state = state == 0 ? 0x9e3779b97f4a7c15UL : state;
                    state ^= state << 13;
                    state ^= state >> 7;
                    state ^= state << 17;
                    save.RandomState = (long)state;
                    int roll = (int)(state % (ulong)total);
                    for (int index = 0; index < definition.OutputItemIds.Length; index++)
                    {
                        roll -= Math.Max(0, index < (definition.OutputWeights?.Length ?? 0)
                            ? definition.OutputWeights[index] : 1000);
                        if (roll < 0) { itemId = definition.OutputItemIds[index]; break; }
                    }
                }
            }
            return Grant(save, itemId, count);
        }

        private static bool Grant(OfflineHomeSave save, int itemId, long count)
        {
            if (itemId <= 0 || count <= 0) return false;
            OfflineHomeItemStack entry = save.Inventory.FirstOrDefault(value => value.ItemId == itemId);
            if (entry == null)
            {
                entry = new OfflineHomeItemStack { ItemId = itemId, Serial = ++save.GrantSerial };
                save.Inventory.Add(entry);
            }
            if (entry.Count > long.MaxValue - count) return false;
            entry.Count += count;
            return true;
        }

        private static bool Consume(OfflineHomeSave save, int itemId, long count)
        {
            OfflineHomeItemStack entry = save.Inventory.FirstOrDefault(value => value.ItemId == itemId);
            if (entry == null || count <= 0 || entry.Count < count) return false;
            entry.Count -= count;
            if (entry.Count == 0) save.Inventory.Remove(entry);
            return true;
        }

        private static void AddReceipt(OfflineHomeSave save, string receipt)
        {
            save.AppliedReceipts.Add(receipt);
        }

        private static OfflineHomeSave Clone(OfflineHomeSave save)
        {
            return JsonUtility.FromJson<OfflineHomeSave>(JsonUtility.ToJson(save));
        }

        private static OfflineHomeMail CloneMail(OfflineHomeMail mail)
        {
            return JsonUtility.FromJson<OfflineHomeMail>(JsonUtility.ToJson(mail));
        }

        private static bool IsValidMail(OfflineHomeMail mail)
        {
            return mail != null && mail.Id > 0 && mail.Status == 0 && mail.Attachments != null
                && mail.Attachments.All(item => item != null && item.ItemId > 0 && item.Count > 0);
        }

        private static bool IsExpired(OfflineHomeMail mail, long nowUnixSeconds)
        {
            return mail.ExpireUnixSeconds > 0 && mail.ExpireUnixSeconds <= nowUnixSeconds;
        }

        private static string LocalDate(long unixSeconds)
        {
            return DateTimeOffset.FromUnixTimeSeconds(Math.Max(0, unixSeconds)).LocalDateTime.ToString("yyyyMMdd");
        }

        private static void ValidateCatalog(OfflineHomeIncomeDefinition[] catalog)
        {
            if (catalog.Length == 0) throw new ArgumentException("At least one official hang-up definition is required.", nameof(catalog));
            var ids = new HashSet<int>();
            foreach (OfflineHomeIncomeDefinition entry in catalog)
            {
                if (entry == null || entry.HangUpId <= 0 || entry.OutputItemId <= 0 || entry.Rule == null
                    || !ids.Add(entry.HangUpId) || !IsFinite(entry.Rule.LevelNum) || entry.Rule.LevelNum < 0
                    || !IsFinite(entry.Rule.HourlyOutput) || !IsFinite(entry.Rule.MaxStoreHours)
                    || entry.FreeFastForwardLimit < 0 || !IsFinite(entry.FreeFastForwardHours)
                    || entry.FreeFastForwardHours < 0 || (entry.FreeFastForwardLimit > 0 && entry.FastHangUpId <= 0))
                    throw new ArgumentException("The official hang-up catalog is invalid.", nameof(catalog));
            }
        }

        private static void ValidateRewardCatalog(OfflineHomeRandomRewardDefinition[] catalog)
        {
            var slots = new HashSet<int>();
            foreach (OfflineHomeRandomRewardDefinition entry in catalog)
                if (entry == null || entry.Slot < 0 || entry.Type < 0 || entry.MaximumCount <= 0
                    || entry.MinimumIntervalSeconds <= 0 || entry.MaximumIntervalSeconds < entry.MinimumIntervalSeconds
                    || !slots.Add(entry.Slot) || entry.Rewards == null
                    || entry.Rewards.Any(value => value == null || value.ItemId <= 0 || value.Count <= 0 || value.Weight <= 0))
                    throw new ArgumentException("The official home random reward catalog is invalid.", nameof(catalog));
        }

        private static bool IsValidJson(string json)
        {
            try
            {
                OfflineHomeSave save = JsonUtility.FromJson<OfflineHomeSave>(json);
                if (save == null || (save.Schema != 1 && save.Schema != 2)) return false;
                UpgradeSave(save);
                return IsValid(save);
            }
            catch { return false; }
        }

        private static bool IsValid(OfflineHomeSave save)
        {
            if (save == null || save.Schema != 2 || save.LastLocalUnixSeconds < 0
                || save.Income == null || save.Inventory == null || save.RandomRewards == null
                || save.FastForwardUsage == null || save.Mail == null || save.Tutorial == null || save.Tutorial.Step < 0
                || save.Tutorial.PendingGuideIds == null || save.AppliedReceipts == null
                || save.Furniture == null || save.HangUps == null || save.Skins == null
                || save.GrantSerial < 0
                || save.AppliedReceipts.Count > ReceiptLimit) return false;
            var hangUpIds = new HashSet<int>();
            foreach (OfflineHomeIncomeEntry entry in save.Income)
                if (entry == null || entry.HangUpId <= 0 || !hangUpIds.Add(entry.HangUpId)
                    || !IsFinite(entry.StoredSeconds) || entry.StoredSeconds < 0 || entry.SettledCount < 0) return false;
            var itemIds = new HashSet<int>();
            var serials = new HashSet<long>();
            foreach (OfflineHomeItemStack entry in save.Inventory)
                if (entry == null || entry.ItemId <= 0 || entry.Count <= 0 || !itemIds.Add(entry.ItemId)
                    || entry.Serial <= 0 || !serials.Add(entry.Serial)) return false;
            var furnitureTypes = new HashSet<int>();
            foreach (OfflineHomeFurnitureEntry entry in save.Furniture)
                if (entry == null || entry.Type < 0 || entry.Type > 13 || !furnitureTypes.Add(entry.Type)) return false;
            var ownedHangUps = new HashSet<int>();
            foreach (OfflineHomeHangUpEntry entry in save.HangUps)
                if (entry == null || entry.HangUpId <= 0 || !ownedHangUps.Add(entry.HangUpId)
                    || entry.Level < 1 || entry.Level > 100000) return false;
            var skinCategories = new HashSet<int>();
            foreach (OfflineHomeSkinSelection entry in save.Skins)
                if (entry == null || entry.Category < 0 || entry.Category > 2
                    || !skinCategories.Add(entry.Category) || entry.SkinItemId <= 0) return false;
            var rewardSlots = new HashSet<int>();
            foreach (OfflineHomeRandomRewardSlot entry in save.RandomRewards)
                if (entry == null || entry.Slot < 0 || !rewardSlots.Add(entry.Slot) || entry.ItemId < 0
                    || entry.Count < 0 || (entry.ItemId == 0) != (entry.Count == 0)
                    || entry.NextRefreshUnixSeconds < 0) return false;
            var fastHangUpIds = new HashSet<int>();
            foreach (OfflineHomeFastForwardUsage entry in save.FastForwardUsage)
                if (entry == null || entry.FastHangUpId <= 0 || !fastHangUpIds.Add(entry.FastHangUpId)
                    || string.IsNullOrWhiteSpace(entry.LocalDate) || entry.FreeUses < 0) return false;
            var mailIds = new HashSet<long>();
            foreach (OfflineHomeMail mail in save.Mail)
                if (mail == null || mail.Id <= 0 || !mailIds.Add(mail.Id) || mail.Status < 0 || mail.Status > 2
                    || mail.Attachments == null || mail.Attachments.Any(item => item == null || item.ItemId <= 0 || item.Count <= 0)) return false;
            return save.AppliedReceipts.All(value => !string.IsNullOrWhiteSpace(value));
        }

        private static long WholeCount(double value)
        {
            if (!IsFinite(value) || value <= 0) return 0;
            return value >= long.MaxValue ? long.MaxValue : (long)value;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
