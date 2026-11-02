using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using GConfig;
using UnityEngine;
using Zjcs.Offline.World.NativeMap14SharedEntities;

namespace Zjcs.Offline.PartnersNpc
{
    // Data consumed by the original NPC PlayerLiteInfo/PlayerInfoData branch.
    // CombatRating comes from the selected NpcFightData IL, including skills.
    public sealed class OfflinePartnerNpcLiteInfo
    {
        public long TargetId { get; internal set; }
        public long ServerId { get; internal set; }
        public long OwnerPlayerId { get; internal set; }
        public int NpcClassId { get; internal set; }
        public int Level { get; internal set; }
        public int SubRank { get; internal set; }
        public int CreatePlanesRank { get; internal set; }
        public int Profession { get; internal set; }
        public int Sex { get; internal set; }
        public string NameKey { get; internal set; }
        public string Icon { get; internal set; }
        public string FigureModelUI { get; internal set; }
        public long CombatRating { get; internal set; }
    }

    public sealed class OfflinePartnerNpcRuntime : IDisposable
    {
        private static readonly string[] BeanNames = { "GameSettings", "NpcDialogueInfo", "NpcInfo", "NpcProfessionDetailInfo", "NpcLevelDetailInfo",
            "PropCfgInfo", "LevelPropDefaultValue", "RolePropGroupInfo", "LevelNumber", "ItemInfo", "SkillInfo", "ProfessionBaseInfo", "EntityPropGroupLevelInfo", "ScoreLevelGroupInfo" };
        private readonly int ownerThread = Thread.CurrentThread.ManagedThreadId;
        private readonly OfflinePartnerNpcAssemblies assemblies;
        private GameObject owner;
        private ConfigManager manager;
        private object parsers, battleFormula, formula, logger, loggerFactory;
        private readonly List<StaticSlot> staticSlots = new List<StaticSlot>();
        private bool disposed;
        public int Norank { get; }

        public OfflinePartnerNpcRuntime(OfficialPartnerNpcRuntimeCatalog catalog)
        {
            assemblies = new OfflinePartnerNpcAssemblies(catalog);
            try
            {
                string root = Path.Combine(Application.streamingAssetsPath, "PartnerNpcShared");
                NativeMap14SharedAssemblies.Verify(File.ReadAllBytes(Path.Combine(root, "Config/Binary/level_prop.g.bytes")), catalog.PropertyBinarySha256);
                owner = new GameObject("Original local NPC configuration");
                manager = owner.AddComponent<ConfigManager>();
                var previousLoader = GConfig.FileLoader.sFileLoader;
                try
                {
                    GConfig.FileLoader.SetupFileLoader(new LocalTables(catalog.Tables));
                    manager.Initialize(root.Replace('\\', '/') + "/", false, 0);
                }
                finally { GConfig.FileLoader.SetupFileLoader(previousLoader); }

                logger = assemblies.Logging.GetType("Microsoft.Extensions.Logging.Abstractions.NullLogger", true).GetProperty("Instance").GetValue(null);
                loggerFactory = assemblies.Logging.GetType("Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory", true).GetField("Instance").GetValue(null);
                Type logType = assemblies.FirstPass.GetType("Framework.Logging.Log", true);
                AddSlot(logType.GetProperty("Logger", BindingFlags.Static | BindingFlags.NonPublic), logger);
                AddSlot(logType.GetProperty("LoggerFactory", BindingFlags.Static | BindingFlags.NonPublic), loggerFactory);
                Type parserType = assemblies.Shared.GetType("Parsers", true);
                parsers = Activator.CreateInstance(parserType, new object[] { manager });
                battleFormula = Activator.CreateInstance(Common("BattleFormulaHandler"));
                formula = Activator.CreateInstance(Common("FormulaHandler"));
                AddSlot(parserType.GetField("Instance"), parsers);
                AddSlot(Common("BattleFormula").GetField("Instance"), battleFormula);
                AddSlot(Common("Formula").GetField("Instance"), formula);

                using (Enter())
                {
                    manager.RegisterTypes(BeanNames.Select(name => Common(name + "Parser")).Concat(new[] { Common("LevelPropParser") }).ToArray());
                    RequiredParser("LevelPropParser"); // Its original Initialize opens the binary; ParseFile is intentionally unimplemented.
                    foreach (string name in BeanNames)
                    {
                        var attribute = Common(name).GetCustomAttribute<ConfigAttribute>();
                        if (attribute == null) throw new InvalidOperationException("Missing original parser attribute: " + name);
                        var request = RequiredParser(name + "Parser").Parse(attribute.LoadPhases, false);
                        if (request != null) manager.Wait(new[] { request });
                        if (request == null || !request.Success) throw new InvalidOperationException("Original NPC parser failed: " + name);
                    }
                    foreach (string name in BeanNames) RequiredParser(name + "Parser").PostParse();
                }
                Norank = Convert.ToInt32(Enum.Parse(Common("SubRank"), "Norank"));
            }
            catch { Dispose(); throw; }
        }

        public int[] NpcClasses()
        {
            CheckAccess();
            using (Enter())
            {
                object parser = RequiredParser("NpcInfoParser");
                return ((IEnumerable)parser.GetType().GetMethod("GetAll", Type.EmptyTypes).Invoke(parser, null)).Cast<object>()
                    .Select(value => Convert.ToInt32(Field(value, "Id"))).ToArray();
            }
        }

        public int[] CombatNpcClasses()
        {
            CheckAccess();
            int[] classes = NpcClasses();
            using (Enter())
            {
                object parser = RequiredParser("NpcLevelDetailInfoParser");
                MethodInfo exists = parser.GetType().GetMethod("ExistData");
                return classes.Where(value => (bool)exists.Invoke(parser, new object[] { value })).ToArray();
            }
        }

        public OfflinePartnerNpcLiteInfo CreateLiteInfo(long targetId, int npcClassId, long ownerPlayerId, int level, int subRank, int createPlanesRank)
        {
            CheckAccess();
            if (targetId <= 0 || ownerPlayerId <= 0) throw new ArgumentOutOfRangeException(nameof(targetId));
            if (level <= 0 || subRank <= 0 || !Enum.IsDefined(Common("SubRank"), subRank) || !Enum.IsDefined(Common("Rank"), createPlanesRank))
                throw new ArgumentOutOfRangeException(nameof(level), "Supply the original requested level/rank; None is not Norank.");
            using (Enter())
            {
                Type targetType = Common("NpcTargetData"), fightType = Common("NpcFightData");
                object target = Activator.CreateInstance(targetType, new object[] { targetId, npcClassId, ownerPlayerId, level,
                    Enum.ToObject(Common("SubRank"), subRank), Enum.ToObject(Common("Rank"), createPlanesRank) });
                object fight = fightType.GetMethod("CreateByNpcTarget", new[] { targetType }).Invoke(null, new[] { target });
                object detail = fightType.GetMethod("GetNpcLevelDetailInfo").Invoke(fight, null);
                if (!(bool)Property(detail, "IsValid")) throw new InvalidOperationException("Original NPC combat detail is missing: " + npcClassId);
                object npc = Property(detail, "NpcInfo");
                return new OfflinePartnerNpcLiteInfo {
                    TargetId = Convert.ToInt64(Property(fight, "TargetId")), ServerId = Convert.ToInt64(Property(target, "ServerId")),
                    OwnerPlayerId = ownerPlayerId, NpcClassId = npcClassId,
                    Level = Convert.ToInt32(Property(fight, "Level")), SubRank = Convert.ToInt32(Property(fight, "SubRank")),
                    CreatePlanesRank = Convert.ToInt32(Property(fight, "CreatePlanesRank")), Profession = Convert.ToInt32(Property(detail, "Profession")),
                    Sex = Convert.ToInt32(Field(npc, "Sex")), NameKey = (string)Field(npc, "NameKey"), Icon = (string)Field(npc, "Icon"),
                    FigureModelUI = (string)Field(npc, "FigureModelUI"), CombatRating = (long)fightType.GetMethod("CalcCombatRating").Invoke(fight, null) };
            }
        }

        public string ProfessionIcon(int profession, bool third)
        {
            CheckAccess();
            using (Enter())
            {
                object parser = RequiredParser("ProfessionBaseInfoParser");
                return (string)parser.GetType().GetMethod(third ? "GetThirdIconPath" : "GetFirstIconPath")
                    .Invoke(parser, new[] { Enum.ToObject(Common("Profession"), profession) });
            }
        }

        private Type Common(string name) => assemblies.Shared.GetType("Common." + name, true);
        private IParser RequiredParser(string name) => manager.GetParser(Common(name)) ?? throw new InvalidOperationException("Original parser was not registered: " + name);
        private static object Field(object owner, string name) => owner.GetType().GetField(name).GetValue(owner);
        private static object Property(object owner, string name) => owner.GetType().GetProperty(name).GetValue(owner);
        private void AddSlot(MemberInfo member, object value)
        {
            if (member == null) throw new InvalidOperationException("Original static service slot is unavailable.");
            staticSlots.Add(new StaticSlot(member, value));
        }
        private IDisposable Enter() => new StaticScope(staticSlots);
        private void CheckAccess()
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfflinePartnerNpcRuntime));
            if (Thread.CurrentThread.ManagedThreadId != ownerThread) throw new InvalidOperationException("NPC runtime belongs to its Unity thread.");
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try { manager?.Dispose(); }
            finally
            {
                if (owner) { if (Application.isPlaying) UnityEngine.Object.Destroy(owner); else UnityEngine.Object.DestroyImmediate(owner); }
                manager = null; owner = null; assemblies.Dispose();
            }
        }

        private sealed class LocalTables : IFileLoader
        {
            private readonly Dictionary<string, byte[]> files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            private string root;
            internal LocalTables(OfficialPartnerNpcRuntimeCatalog.Table[] tables)
            {
                foreach (var table in tables)
                {
                    if (table == null || !table.Data || string.IsNullOrEmpty(table.Location)) throw new InvalidOperationException("NPC table binding is incomplete.");
                    byte[] bytes = table.Data.bytes;
                    NativeMap14SharedAssemblies.Verify(bytes, table.Sha256);
                    files.Add(table.Location, bytes);
                }
            }
            public void Initialize(ConfigManager value) { root = value.RootDir; }
            private bool TryGet(string file, out byte[] bytes)
            {
                bytes = null;
                return file.StartsWith(root, StringComparison.Ordinal) && files.TryGetValue(file.Substring(root.Length).Replace('\\', '/'), out bytes);
            }
            public bool Exists(string file) => TryGet(file, out _);
            public void LoadFile(string file, bool isBinary, bool async, ConfigRequestBase request)
            {
                if (!isBinary) throw new InvalidOperationException("Original NPC tables require binary input: " + file);
                if (!TryGet(file, out var bytes)) throw new FileNotFoundException("Original NPC local table is unbound.", file);
                request.EndLoading(bytes);
            }
        }
        private sealed class StaticSlot
        {
            private readonly MemberInfo member;
            internal readonly object Value;
            internal StaticSlot(MemberInfo member, object value) { this.member = member; Value = value; }
            internal object Read() => member is FieldInfo field ? field.GetValue(null) : ((PropertyInfo)member).GetValue(null);
            internal void Write(object value) { if (member is FieldInfo field) field.SetValue(null, value); else ((PropertyInfo)member).SetValue(null, value); }
        }
        private sealed class StaticScope : IDisposable
        {
            private readonly List<StaticSlot> slots;
            private readonly object[] previous;
            private int applied;
            internal StaticScope(List<StaticSlot> slots)
            {
                this.slots = slots; previous = new object[slots.Count];
                try { for (; applied < slots.Count; applied++) { previous[applied] = slots[applied].Read(); slots[applied].Write(slots[applied].Value); } }
                catch { Dispose(); throw; }
            }
            public void Dispose() { while (applied > 0) { applied--; slots[applied].Write(previous[applied]); } }
        }
    }
}
