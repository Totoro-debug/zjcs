using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Zjcs.Offline.World.NativeMap14SharedEntities
{
    // Original client construction uses the real recovered selector/injector
    // and the pinned DryIoc implementation. No injection receiver is substituted.
    public sealed class MainlineSharedClientServices : IDisposable
    {
        private PropertyInfo current;
        private object previous;
        private IDisposable provider;
        private readonly List<(FieldInfo Field, object Previous, object Value)> formulas
            = new List<(FieldInfo, object, object)>();
        public object Manager { get; private set; }

        public MainlineSharedClientServices(NativeMap14SharedAssemblies shared)
        {
            if (shared == null) throw new ArgumentNullException(nameof(shared));
            try
            {
                var core = shared.Load("Framework.Core");
                var dry = shared.Load("DryIoc");
                var framework = shared.Load("Framework.FirstPass.Runtime");
                var selector = Activator.CreateInstance(core.GetType("Framework.DependencyInjection.DryIoc.InjectAttributeSelector", true));
                var select = selector.GetType().GetMethod("Properties");
                var properties = select.Invoke(selector, new[] { select.GetParameters()[0].DefaultValue });
                var rulesType = dry.GetType("DryIoc.Rules", true);
                var rules = rulesType.GetField("Default").GetValue(null);
                rules = rulesType.GetMethods().Single(method => method.Name == "With" && method.GetParameters().Length == 3)
                    .Invoke(rules, new[] { null, null, properties });
                var containerType = dry.GetType("DryIoc.Container", true);
                provider = (IDisposable)containerType.GetConstructor(new[] { rulesType, dry.GetType("DryIoc.IScopeContext", true) })
                    .Invoke(new[] { rules, null });
                var injector = Activator.CreateInstance(core.GetType("Framework.DependencyInjection.DryIoc.DryIocInstanceInjector", true), provider);
                containerType.GetMethod("Use", new[] { typeof(Type), typeof(object) }).Invoke(provider, new[] {
                    core.GetType("Framework.DependencyInjection.Abstractions.IInstanceInjector", true), injector });
                current = framework.GetType("Framework.DependencyInjection.ApplicationScope", true).GetProperty("Current");
                previous = current.GetValue(null);
                current.SetValue(null, provider);
                // Preserve the formula binding order in original UtilsHandlerBinder.BindAll.
                foreach (var name in new[] { "BattleFormula", "Formula" })
                {
                    var field = shared.CommonType(name).GetField("Instance");
                    var value = Activator.CreateInstance(shared.CommonType(name + "Handler"));
                    formulas.Add((field, field.GetValue(null), value));
                    field.SetValue(null, value);
                }
                Manager = shared.Shared.GetType("MeManager", true).GetProperty("Instance").GetValue(null);
            }
            catch { Dispose(); throw; }
        }

        public void Dispose()
        {
            if (provider == null) return;
            try
            {
                for (int i = formulas.Count - 1; i >= 0; i--)
                {
                    var binding = formulas[i];
                    if (ReferenceEquals(binding.Field.GetValue(null), binding.Value))
                        binding.Field.SetValue(null, binding.Previous);
                }
                if (current != null && ReferenceEquals(current.GetValue(null), provider))
                    current.SetValue(null, previous);
            }
            finally
            {
                provider.Dispose();
                provider = null;
                Manager = null;
                current = null;
                previous = null;
                formulas.Clear();
            }
        }
    }
}
