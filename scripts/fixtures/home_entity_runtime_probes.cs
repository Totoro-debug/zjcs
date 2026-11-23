using UnityEngine;
using Zjcs.Official.HomeEntityRuntime;
using Zjcs.Official.HomeUI;

public sealed class HomeEntityRuntimeProbe : MonoBehaviour { }
public sealed class HomeEntityRuntimeProxy : MonoBehaviour, IFurnitureProxy<HomeEntityRuntimeProbe>
{
    public HomeEntityRuntimeProbe Value;
    public HomeEntityRuntimeProbe GetComponent(EFurniture type) => type == EFurniture.StorageBox ? Value : null;
}
