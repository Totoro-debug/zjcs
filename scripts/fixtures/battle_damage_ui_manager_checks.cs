using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Official.BattleDamageUI;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;
using Object = UnityEngine.Object;

public static class DamageUiManagerChecks
{
    [Serializable] sealed class Bindings { public Binding[] prefabs; }
    [Serializable] sealed class Binding { public string guid, resource; }
    sealed class Damage : FightDamageComponent
    {
        public DamageResultParameter Result;
        public override DamageResultParameter DamageResult => Result;
        public override EcEntityIdentifier TargetId => new EcEntityIdentifier(1, 1);
        public override EcEntityIdentifier SkillId => new EcEntityIdentifier(2, 1);
        public override bool IsDamageTriggerStatus => false;
    }
    sealed class Entity : ECViewEntityData
    {
        public EcEntityIdentifier Identity;
        public Damage Damage;
        public readonly BattleRoleFightViewData Role = new BattleRoleFightViewData();
        public override EcEntityIdentifier Id => Identity;
        public override int ClassId => 1;
        public override T GetComponent<T>()
        {
            object data = typeof(T).IsInstanceOfType(Damage) ? (object)Damage
                : typeof(T).IsInstanceOfType(Role) ? Role : null;
            return data == null ? default : (T)data;
        }
    }
    sealed class Context : CharacterDamageUIContext
    {
        public readonly Dictionary<EcEntityIdentifier, MyEntity> Entities = new Dictionary<EcEntityIdentifier, MyEntity>();
        public readonly List<CharacterDamageUIRole> Roles = new List<CharacterDamageUIRole>();
        public Transform Root;
        public FightDamageSettings Config;
        public bool Pool = true;
        public int Loads, Releases, AsyncLoads;
        public Action<MyFightSkill> Pending;
        public CharacterDamageUIWarmupSettings Warmup;
        public Dictionary<string, string> Paths;
        public override Transform DamageFloatsRoot => Root;
        public override FightDamageSettings Settings => Config;
        public override bool UseGameObjectPool => Pool;
        public override IEnumerable<CharacterDamageUIRole> Characters => Roles;
        public override MyEntity GetEntity(EcEntityIdentifier id) => Entities.TryGetValue(id, out var entity) ? entity : null;
        public override GameObject LoadPrefab(string guid) { Loads++; return Resources.Load<GameObject>(Paths[guid]); }
        public override void ReleasePrefabs() { Releases++; }
        public int? CurrentStage = 101;
        public override int? StageType => CurrentStage;
        public override CharacterDamageUIWarmupSettings WarmupSettings => Warmup;
        public override void GetSkillPrefabAsync(EcEntityIdentifier id, Action<MyFightSkill> ready) { AsyncLoads++; Pending = ready; }
        public override ElementType GetSkillElementType(MyFightDamage damage) => damage.Component.DamageResult.SkillElementType;
        public void Add(CharacterDamageUIRole role) { Roles.Add(role); CharacterAdded(role); }
        public void Remove(CharacterDamageUIRole role) { Roles.Remove(role); CharacterRemoved(role); }
        public void Replay(EcEntityIdentifier role, EcEntityIdentifier skill, bool extra = false)
            => SkillReplayed(new CharacterDamageUISkillReplay(role, skill, extra));
    }
    public static IEnumerator Check(Camera camera)
    {
        var settings = Resources.Load<FightDamageSettings>("Combat/DamageUI/FightDamageSettings");
        Require(settings != null && settings.EffectUIPoolPresents.Count == 13, "Native damage settings and thirteen GUID proxies.");
        Require(settings.EffectUIPoolPresents[0].PoolInitSize == 32 && settings.DamageUISpacing.y == -.1f,
            "Original native pool counts and stacking interval.");
        var context = new Context { Root = new GameObject("Original manager damage root").transform, Config = settings,
            Paths = JsonUtility.FromJson<Bindings>(Resources.Load<TextAsset>("Combat/DamageUI/prefab-bindings").text)
                .prefabs.ToDictionary(item => item.guid, item => item.resource),
            Warmup = JsonUtility.FromJson<CharacterDamageUIWarmupSettings>(Resources.Load<TextAsset>("Combat/DamageUI/warmup-settings").text) };
        var actor = Actor(context, new EcEntityIdentifier(1, 1));
        var role = new CharacterDamageUIRole(actor.GetComponent<MyFightRoleFight>());
        var actors = new List<MyEntity> { actor };
        context.Add(role);
        var manager = new GameObject("Original damage UI manager").AddComponent<CharacterDamageUIManager>();
        manager.Context = context; manager.Init();
        Require(context.Loads == 13 && context.Root.childCount == 198, "Original stage 101 pool warmup overrides 100/50, remaining defaults.");
        foreach (int? stage in new int?[] { null, 999 })
        {
            var defaults = new Context { Root = new GameObject("Original default warmup").transform, Config = settings,
                Paths = context.Paths, Warmup = context.Warmup, CurrentStage = stage };
            var defaultManager = new GameObject("Original default manager").AddComponent<CharacterDamageUIManager>();
            defaultManager.Context = defaults; defaultManager.Init();
            Require(defaults.Root.childCount == 112, "Absent fight or unconfigured stage keeps original default pool counts.");
            defaultManager.enabled = false; defaultManager.Release();
            Object.Destroy(defaultManager.gameObject); Object.Destroy(defaults.Root.gameObject);
        }
        foreach (var element in context.Root.GetComponentsInChildren<ElementConfrontationUI>(true))
            element.ElementConfrontThreshold = context.Warmup.ElementConfrontThreshold;
        Require(Active(context).Length == 0, "Warmup leaves every pooled UI inactive.");
        var damageGo = new GameObject("Consumed damage receipt");
        var damageEntity = damageGo.AddComponent<MyEntity>();
        var data = new Damage(); damageEntity.Entity = new Entity { Identity = new EcEntityIdentifier(3, 1), Damage = data };
        var damage = damageGo.AddComponent<MyFightDamage>();
        damage.Bind();
        // Fire the same original role notification that MyFightDamage.OnStart emits.
        void Fire(long value, DamageTypeData flags = default, ElementType element = ElementType.Fire,
            long shield = 0, DamageResultType result = DamageResultType.Damage)
        {
            data.Result = new DamageResultParameter { DamageValue = value, ShieldValue = shield,
                ResultType = result, SkillElementType = element, DamageTypeData = flags };
            role.Fight.FireDamageEvent(damage);
        }
        var matrix = new[] {
            (EffectUIType.NormalDamageUI, new DamageTypeData(), ElementType.Fire),
            (EffectUIType.AbyssNormalDamageUI, new DamageTypeData(), ElementType.Abyss),
            (EffectUIType.CritDamageUI, new DamageTypeData { IsCrit = true, IsBlock = true }, ElementType.Fire),
            (EffectUIType.AbyssCritDamageUI, new DamageTypeData { IsCrit = true }, ElementType.Abyss),
            (EffectUIType.BlockDamageUI, new DamageTypeData { IsBlock = true }, ElementType.Fire),
            (EffectUIType.AbyssBlockDamageUI, new DamageTypeData { IsBlock = true }, ElementType.Abyss),
            (EffectUIType.ImmunityDamageUI, new DamageTypeData { IsImmunity = true, IsDodge = true, IsBlinding = true, IsCrit = true }, ElementType.Fire),
            (EffectUIType.DodgeDamageUI, new DamageTypeData { IsDodge = true, IsBlinding = true, IsCrit = true }, ElementType.Fire),
            (EffectUIType.BlindingDamageUI, new DamageTypeData { IsBlinding = true, IsCrit = true }, ElementType.Fire) };
        int count = 0;
        Time.timeScale = 0;
        foreach (var entry in matrix)
        {
            if (count != 0)
            {
                actor = Actor(context, new EcEntityIdentifier(1, count + 1)); actors.Add(actor);
                role = new CharacterDamageUIRole(actor.GetComponent<MyFightRoleFight>()); context.Add(role);
            }
            actor.transform.position = new Vector3((count % 3 - 1) * 3, 3 - count / 3 * 2.5f, 0);
            Fire(-1234567, entry.Item2, entry.Item3);
            var ui = Active(context).Last();
            Require(ui.Type == entry.Item1, "Original type priority " + entry.Item1);
            if (count == 2)
            {
                var blockColor = settings.ElementDamageColors.Single(color => color.ElementType == ElementType.Fire).Block.TextColor;
                Require(ui.GetComponentsInChildren<TextMesh>(true).Any(text => ColorClose(text.color, blockColor)),
                    "Crit prefab uses Block color when both flags are set. expected=" + blockColor.linear.ToString("F6")
                    + " actual=" + string.Join(";", ui.GetComponentsInChildren<TextMesh>(true).Select(text => text.color.ToString("F6"))));
            }
            count++;
        }
        Time.timeScale = 1; yield return new WaitForSeconds(.35f); Time.timeScale = 0;
        DamageUiRunner.Capture(camera, "damage-ui-manager-priorities.png");
        Require(Active(context).Length == 9, "Exactly one UI for each role notification.");
        yield return new WaitForSecondsRealtime(.2f);
        Require(Active(context).Length == 9, "Manager lifetime uses original scaled clock.");
        Time.timeScale = 1; yield return new WaitForSeconds(3);
        Require(Active(context).Length == 0, "All nine manager completions return to pool and clear identity.");
        int warmupCount = context.Root.childCount;
        actor.transform.position = Vector3.zero;
        Fire(-5000000000L);
        Require(Active(context).Single().GetComponentsInChildren<TextMesh>(true).Any(text => text.text == "500000万"),
            "64-bit receipt retains original battle number formatting.");
        Fire(-100, shield: 25);
        Require(Active(context).Last().GetComponentsInChildren<TextMesh>(true).Any(text => text.text == "125"), "ShieldValue is subtracted before display.");
        Require(context.Root.childCount == warmupCount, "Completed prefabs were reused rather than instantiated again.");
        var first = Active(context)[0]; var second = Active(context)[1];
        Require(Math.Abs(first.transform.position.y - second.transform.position.y - second.Height - settings.DamageUISpacing.y) < .0001f,
            "Previous UI is stacked by next native height plus configured spacing.");
        Fire(100, new DamageTypeData { IsImmunity = true }, result: DamageResultType.Cure);
        Require(Active(context).Last().Type == EffectUIType.CureUI, "Positive DamageValue selects Cure before immunity.");
        int before = Active(context).Length;
        damage.myHideDamage = true; Fire(-100); Require(Active(context).Length == before, "Hidden damage suppresses nonzero UI.");
        damage.myHideDamage = false; damage.myShowZeroDamage = false; Fire(0);
        Require(Active(context).Length == before, "ShowZero false suppresses zero UI.");
        var filter = actor.gameObject.AddComponent<CustomDamageUITypeFilter>(); filter.IsCrit = true;
        damage.myShowZeroDamage = true; Fire(0, new DamageTypeData { IsCrit = true });
        Require(Active(context).Length == before, "Original custom crit filter suppresses zero UI.");
        filter.IsCrit = false; Fire(0); Require(Active(context).Length == ++before, "Allowed zero retains original prefab.");
        filter.IsCrit = true; data.Result = new DamageResultParameter { IsElementAffect = true, ElementConfrontation = 1,
            DamageTypeData = new DamageTypeData { IsCrit = true } };
        role.Fight.FireDamageEvent(damage); Require(Active(context).Length == ++before, "Element affect bypasses zero filter.");
        role.StatusDamage(200, true); Require(Active(context).Last().Type == EffectUIType.NormalDamageUI,
            "Original status healing uses Normal prefab and StatusCure color."); before++;
        Require(Active(context).Last().GetComponentsInChildren<TextMesh>(true).Any(text => ColorClose(text.color, settings.StatusCureColor.TextColor)),
            "All status cure number colors come from original settings.");
        role.SkillCostHp(new EcEntityIdentifier(2, 1), 1000, 950); before++;
        Require(Active(context).Last().GetComponentsInChildren<TextMesh>(true).Any(text => text.text == "50" && ColorClose(text.color, settings.SkillCostDamageColor.TextColor)),
            "HP skill cost displays hp minus preHp with original cost color.");
        role.DisperseStatus(); Require(Active(context).Last().Type == EffectUIType.DisperseDamageUI, "Original disperse notification."); before++;
        role.StatusTriggerCustomAction(EStatusCustomAction.Unflinching);
        Require(Active(context).Last().Type == EffectUIType.CustomActionUI, "Original status custom-action notification."); before++;
        var skillId = new EcEntityIdentifier(2, 1);
        var skillEntity = Actor(context, skillId); var skill = skillEntity.gameObject.AddComponent<MyFightSkill>();
        context.Replay(role.Id, skillId, true); Require(Active(context).Length == before, "Extra replay is hidden.");
        skill.ShowSkillReplay = false; context.Replay(role.Id, skillId); Require(Active(context).Length == before, "Native ShowSkillReplay false is honored.");
        skill.ShowSkillReplay = true; context.Replay(role.Id, skillId);
        Require(Active(context).Last().Type == EffectUIType.ResetSkillCDUI, "Loaded original skill replay notification."); before++;
        context.Entities.Remove(skillId); context.Replay(role.Id, skillId);
        Require(context.AsyncLoads == 1 && Active(context).Length == before, "Missing skill waits for original prefab lookup.");
        context.Pending(skill); Require(Active(context).Length == ++before, "Asynchronous prefab replay flag creates one UI.");
        context.Remove(role); role.StatusDamage(-100, false); role.DisperseStatus(); Fire(-100);
        Require(Active(context).Length == before, "Removed role has no remaining damage or shared subscriptions.");
        context.Add(role); role.DisperseStatus(); Require(Active(context).Length == ++before, "Re-added role has exactly one subscription.");
        manager.enabled = false; role.DisperseStatus(); context.Replay(role.Id, skillId);
        Require(Active(context).Length == before && context.AsyncLoads == 1, "Disabled manager detaches role and root notifications.");
        manager.enabled = true; role.DisperseStatus(); Require(Active(context).Length == ++before, "Re-enabled manager binds once.");
        Time.timeScale = 1; yield return new WaitForSeconds(3);
        Require(Active(context).Length == 0, "Stacked status/replay UI complete and return normally.");
        manager.enabled = false; manager.Release(); Require(context.Releases == 1, "Original resource lease release.");
        Object.Destroy(manager.gameObject); Object.Destroy(context.Root.gameObject);
        // Exercise the original non-pool branch through the same public interface.
        context.Root = new GameObject("Original nonpool root").transform; context.Pool = false;
        manager = new GameObject("Original nonpool manager").AddComponent<CharacterDamageUIManager>(); manager.Context = context; manager.Init();
        Require(context.Root.childCount == 0, "Nonpool branch does not preinstantiate.");
        role.DisperseStatus(); Require(Active(context).Single().Type == EffectUIType.DisperseDamageUI, "Nonpool native instantiation.");
        yield return new WaitForSeconds(3); Require(context.Root.childCount == 0, "Nonpool completion destroys the native object.");
        manager.enabled = false; manager.Release(); Object.Destroy(manager.gameObject); Object.Destroy(context.Root.gameObject);
        foreach (var item in actors) Object.Destroy(item.gameObject);
        Object.Destroy(skillEntity.gameObject); Object.Destroy(damageGo);
        Debug.Log("Original damage manager passed: native settings, 198 warmup instances, priorities, colors, shield/64-bit values, filtering, subscriptions, replay, stack, pooling and nonpool completion.");
    }
    static MyEntity Actor(Context context, EcEntityIdentifier id)
    {
        var actor = new GameObject("Role " + id).AddComponent<MyEntity>(); actor.Entity = new Entity { Identity = id };
        actor.gameObject.AddComponent<MyFightRoleFight>(); context.Entities.Add(id, actor); return actor;
    }
    static CharacterDamageUI[] Active(Context context) => context.Root.GetComponentsInChildren<CharacterDamageUI>()
        .Where(ui => ui.gameObject.activeSelf).OrderBy(ui => ui.transform.GetSiblingIndex()).ToArray();
    static bool ColorClose(Color actual, Color configured)
    {
        // TextMesh stores Color32 after the original linear RGB conversion;
        // DamageNumberUI separately retains the native animated alpha.
        Color expected = (Color32)configured.linear;
        return Math.Abs(actual.r-expected.r) + Math.Abs(actual.g-expected.g) + Math.Abs(actual.b-expected.b) < .0001f;
    }
    static void Require(bool value, string reason) { if (!value) throw new InvalidOperationException("Damage manager: " + reason); }
}
