using System;
namespace Zjcs.Official.HomeCamera
{
public interface IUIBloomManager { bool BloomActive { get; }
void RegisterActiveListener(Action<bool> callback);
void UnregisterActiveListener(Action<bool> callback); }
}
