using UnityEngine;

public sealed class HomeNativeFactoryProbe : MonoBehaviour
{
    public object Dependency;
    public object AwakeDependency, EnableDependency;
    public int AwakeCount, EnableCount;
    public Transform AwakeParent;
    void Awake() { AwakeCount++; AwakeDependency = Dependency; AwakeParent = transform.parent; }
    void OnEnable() { EnableCount++; EnableDependency = Dependency; }
}
