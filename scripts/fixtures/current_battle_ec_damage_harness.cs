using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityEngine
{
    public class Object { public static implicit operator bool(Object value) => value != null; }
    public class TooltipAttribute : Attribute { public TooltipAttribute(string text) { } }
    public class SerializeField : Attribute { }
    public class Component : Object
    {
        public GameObject gameObject;
        public T GetComponent<T>() => gameObject.GetComponent<T>();
        public T GetComponentInParent<T>(bool inactive) => gameObject.GetComponent<T>();
    }
    public class Transform : Component { }
    public class GameObject : Object
    {
        public readonly Dictionary<Type, object> Components = new Dictionary<Type, object>();
        public Transform transform;
        public GameObject() { transform = new Transform { gameObject = this }; }
        public T GetComponent<T>() => Components.TryGetValue(typeof(T), out var value) ? (T)value : default;
        public T Add<T>(T value) where T : Component { value.gameObject = this; Components[typeof(T)] = value; return value; }
    }
}
namespace Zjcs.Official.EntityTransformViews { }
namespace Zjcs.Official.World.NativeTerrain.Common { }
namespace Zjcs.Official.EntityViews
{
    using UnityEngine;
    public abstract class ECViewEntityData { public abstract T GetComponent<T>(); }
    public abstract class ECViewBase
    {
        protected GameObject mOwner;
        protected ECViewEntityData mEntity;
        protected T GetComponent<T>() => mEntity.GetComponent<T>();
        protected virtual void Load() { }
        public void TestLoad(GameObject owner, ECViewEntityData entity) { mOwner = owner; mEntity = entity; Load(); }
    }
}
namespace Zjcs.Official.EntityLifecycle
{
    using UnityEngine;
    public class MyContainer : Component { }
    public class MyEntity : Component { public int Id; public MyContainer myContainer = new MyContainer(); }
}
namespace Zjcs.Official.StoryPlayables { public class MyPlayable { } }
namespace Zjcs.Official.StoryPlayableManager
{
    using UnityEngine;
    using Zjcs.Official.StoryPlayables;
    public interface IMyPlayableManager
    {
        MyPlayable Play(GameObject graph, GameObject source, GameObject target, float speed,
            GameObject parent, Dictionary<string, object> parameters);
    }
}
namespace Zjcs.Official.BattleSkillPlayables
{
    using UnityEngine;
    using Zjcs.Official.EntityLifecycle;
    public interface IFightGetUsePlayable { void TryGetPlayables(ref HashSet<GameObject> playables); }
    public class FightSkillComponent { public int SrcEntityId; }
    public class MyFightSkill : Component { public FightSkillComponent Component; }
    public class DamageTypeData { public bool IsBlock, IsCrit; }
    public class DamageResultParameter { public DamageTypeData DamageTypeData = new DamageTypeData(); }
    public class FightDamageComponent
    {
        public int TargetId, SkillId;
        public bool IsDamageTriggerStatus;
        public DamageResultParameter DamageResult = new DamageResultParameter();
    }
    public class MyFightDamage : Component
    {
        public bool myShowZeroDamage, myHideDamage;
        public GameObject myCommonPlayable, myDamagePlayable, myBlockPlayable, myCritPlayable, myTriggerStatusPlayable;
    }
    public class MyFightRoleFight : Component
    {
        public Action<MyFightDamage> Damage, Hit;
        public void FireDamageEvent(MyFightDamage carrier) => Damage?.Invoke(carrier);
        public void FireHitEvent(MyFightDamage carrier) => Hit?.Invoke(carrier);
    }
    public interface IFightSkillViewServices { MyEntity GetEntity(int id); }
    public abstract class BattlePlayableOverrideView { public abstract GameObject Resolve(string tag, GameObject original); }
    public static class CurrentFightViewLookup
    {
        public static BattlePlayableOverrideView Override;
        public static BattlePlayableOverrideView GetOverride(MyEntity entity) => Override;
    }
}

public static class CurrentBattleEcDamageHarness
{
    private sealed class State : Zjcs.Official.EntityViews.ECViewEntityData
    {
        public Zjcs.Official.BattleSkillPlayables.FightDamageComponent Damage;
        public override T GetComponent<T>() => (T)(object)Damage;
    }
    private sealed class Host : Zjcs.Official.BattleSkillPlayables.IFightSkillViewServices,
        Zjcs.Official.StoryPlayableManager.IMyPlayableManager
    {
        public readonly Dictionary<int, Zjcs.Official.EntityLifecycle.MyEntity> Entities = new Dictionary<int, Zjcs.Official.EntityLifecycle.MyEntity>();
        public readonly List<UnityEngine.GameObject> Played = new List<UnityEngine.GameObject>();
        public readonly List<string> Notifications = new List<string>();
        public UnityEngine.GameObject DamageRoot, Target, Source;
        public Zjcs.Official.EntityLifecycle.MyEntity GetEntity(int id) => Entities.TryGetValue(id, out var value) ? value : null;
        public Zjcs.Official.StoryPlayables.MyPlayable Play(UnityEngine.GameObject graph, UnityEngine.GameObject source,
            UnityEngine.GameObject target, float speed, UnityEngine.GameObject parent, Dictionary<string, object> parameters)
        {
            Need(Notifications.SequenceEqual(new[] { "damage", "hit" }), "Role notifications must precede playables");
            Need(source == Target && target == Source && speed == 1 && parent == null
                && (UnityEngine.GameObject)parameters["Damage"] == DamageRoot, "Actual damage parameters and direction");
            Played.Add(graph); return null;
        }
    }
    private sealed class Override : Zjcs.Official.BattleSkillPlayables.BattlePlayableOverrideView
    {
        public UnityEngine.GameObject Replacement = new UnityEngine.GameObject();
        public readonly List<string> Tags = new List<string>();
        public override UnityEngine.GameObject Resolve(string tag, UnityEngine.GameObject original) { Tags.Add(tag); return Replacement; }
    }
    private static void Need(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Field(object target, string name, object value) => target.GetType().GetField(name,
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(target, value);
    public static void Run()
    {
        int cases = 0;
        for (int mask = 0; mask < 4; mask++) for (int nulls = 0; nulls < 32; nulls++)
        for (int trigger = 0; trigger < 2; trigger++) for (int missing = 0; missing < 4; missing++)
        {
            var host = new Host();
            var root = new UnityEngine.GameObject(); host.DamageRoot = root;
            root.Add(new Zjcs.Official.EntityLifecycle.MyEntity { Id = 4 });
            var carrier = root.Add(new Zjcs.Official.BattleSkillPlayables.MyFightDamage());
            for (int id = 1; id <= 3; id++)
            {
                var go = new UnityEngine.GameObject();
                host.Entities[id] = go.Add(new Zjcs.Official.EntityLifecycle.MyEntity { Id = id });
            }
            host.Target = host.Entities[1].gameObject; host.Source = host.Entities[3].gameObject;
            host.Target.Add(new Zjcs.Official.BattleSkillPlayables.MyFightRoleFight { Damage = value => {
                Need(value == carrier && value.myShowZeroDamage && !value.myHideDamage, "Current EC visibility reaches actual event carrier");
                host.Notifications.Add("damage"); } });
            host.Source.Add(new Zjcs.Official.BattleSkillPlayables.MyFightRoleFight { Hit = value => {
                Need(value == carrier, "Same damage identity reaches source"); host.Notifications.Add("hit"); } });
            host.Entities[2].gameObject.Add(new Zjcs.Official.BattleSkillPlayables.MyFightSkill {
                Component = new Zjcs.Official.BattleSkillPlayables.FightSkillComponent { SrcEntityId = 3 } });
            if (missing > 0) host.Entities.Remove(missing);
            var state = new State { Damage = new Zjcs.Official.BattleSkillPlayables.FightDamageComponent {
                TargetId = 1, SkillId = 2, IsDamageTriggerStatus = trigger != 0 } };
            state.Damage.DamageResult.DamageTypeData.IsBlock = (mask & 1) != 0;
            state.Damage.DamageResult.DamageTypeData.IsCrit = (mask & 2) != 0;
            var view = new Zjcs.Official.BattleSkillPlayables.ECFightDamageView { ViewServices = host, MyPlayableManager = host };
            var names = new[] { "mCommonPlayable", "mDamagePlayable", "mBlockPlayable", "mCritPlayable", "mTriggerStatusPlayable" };
            var graphs = names.Select((name, index) => (nulls & (1 << index)) == 0 ? new UnityEngine.GameObject() : null).ToArray();
            for (int i = 0; i < names.Length; i++) Field(view, names[i], graphs[i]);
            view.TestLoad(root, state);
            var expected = missing > 0 ? new UnityEngine.GameObject[0]
                : new[] { graphs[0], graphs[(mask & 1) != 0 ? 2 : (mask & 2) != 0 ? 3 : 1], trigger != 0 ? graphs[4] : null }.Where(value => value != null).ToArray();
            Need(host.Played.SequenceEqual(expected), "Current block/crit/null branches must never fall through");
            Need(host.Notifications.SequenceEqual(missing == 1 ? new string[0] : missing > 1 ? new[] { "damage" } : new[] { "damage", "hit" }), "Missing entity branch order");
            if (missing == 0)
            {
                host.Played.Clear(); host.Notifications.Clear();
                var replacement = new Override(); Zjcs.Official.BattleSkillPlayables.CurrentFightViewLookup.Override = replacement;
                view.TestLoad(root, state);
                Need(replacement.Tags.SequenceEqual(new[] { "myCommonPlayable", (mask & 1) != 0 ? "myBlockPlayable" : (mask & 2) != 0 ? "myCritPlayable" : "myDamagePlayable" }
                    .Concat(trigger != 0 ? new[] { "myTriggerStatusPlayable" } : new string[0])), "Override resolves original ordered tags even for null defaults");
                Need(host.Played.All(value => value == replacement.Replacement) && host.Played.Count == 2 + trigger, "Resolved override graphs play once");
                Zjcs.Official.BattleSkillPlayables.CurrentFightViewLookup.Override = null;
            }
            cases++;
        }
        Console.WriteLine("Current official EC damage: " + cases + " null-field, block/crit, trigger and missing-entity cases; 256 ordered override cases passed.");
    }
}
