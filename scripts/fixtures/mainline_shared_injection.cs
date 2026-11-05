using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using DryIoc;
using DryIoc.ImTools;
using Microsoft.Extensions.DependencyInjection;

// Original Framework.Core injection subset, recovered from selected 884 AOT.
// Inject: 0xb872300; instance constructor: 0xb86cbc8; instance call: 0xb872c30.
// Selector constructor: 0xb872e7c; static type: 0xb872f04.
// Native bodies and original metadata are verified by the generation tool.
namespace Framework.DependencyInjection
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class InjectAttribute : Attribute { }
    public static class ServiceProviderExtensions
    {
        public static void Inject(this IServiceProvider serviceProvider, object instance)
        {
            serviceProvider.GetService<Abstractions.IInstanceInjector>().Inject(instance);
        }
    }
}
namespace Framework.DependencyInjection.Abstractions
{
    public interface IInstanceInjector { void Inject(object instance); }
}
namespace Framework.DependencyInjection.DryIoc
{
    public class DryIocInstanceInjector : Abstractions.IInstanceInjector
    {
        private readonly IResolverContext mContext;
        public DryIocInstanceInjector(IResolverContext context) { mContext = context; }
        public void Inject(object instance) { mContext.InjectPropertiesAndFields<object>(instance); }
    }
    public class InjectAttributeSelector
    {
        private static Type sInjectAttributeType = typeof(InjectAttribute);
        private readonly ConcurrentDictionary<Type, PropertyOrFieldServiceInfo[]> mInjectCache =
            new ConcurrentDictionary<Type, PropertyOrFieldServiceInfo[]>();

        // 0xb872cb0, with nested bodies 0xb873010 through 0xb8734b4.
        public PropertiesAndFieldsSelector Properties(IfUnresolved ifUnresolved = IfUnresolved.ReturnDefaultIfNotRegistered)
        {
            ServiceDetails details = ifUnresolved == IfUnresolved.Throw ? ServiceDetails.Default :
                ifUnresolved == IfUnresolved.ReturnDefault ? ServiceDetails.IfUnresolvedReturnDefault :
                ServiceDetails.IfUnresolvedReturnDefaultIfNotRegistered;
            PropertiesAndFields.GetServiceInfo serviceInfo = (m, r) => PropertyOrFieldServiceInfo.Of(m).WithDetails(details);
            return req =>
            {
                Type type = req.ImplementationType;
                if (type == null) return null;
                PropertyOrFieldServiceInfo[] result;
                if (mInjectCache.TryGetValue(type, out result)) return result;
                var properties = type.GetMembers(x => x.DeclaredProperties, true).ToArrayOrSelf();
                result = properties.Match(serviceInfo, req,
                    (getInfo, request, property) => Attribute.IsDefined(property, sInjectAttributeType),
                    (getInfo, request, property) => getInfo(property, request));
                mInjectCache.TryAdd(type, result);
                return result;
            };
        }
    }
}
