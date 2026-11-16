using System.Threading.Tasks;
namespace Zjcs.Official.HomeCamera
{
public interface IPersistentState<T> { T State { get; } Task WriteStateAsync(); }
public interface IDevice { PerformanceLevel GetPerformanceLevel(); }
}
