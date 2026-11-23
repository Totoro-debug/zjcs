using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Zjcs.Official.HomeFurniture;
using Zjcs.Official.HomeSceneInput;
using Zjcs.Presentation.Home;

public sealed class HomeStoragePointerValidation : IHomeSceneClickPointer
{
    public bool LeftDown { get; private set; }
    public bool LeftUp { get; private set; }
    public Vector2 Position { get; private set; }
    public int Width => Screen.width;
    public int Height => Screen.height;

    public StoreBoxFurniture Center(HomeFeatureRuntime home)
    {
        var camera = home.Scene.Camera;
        var furniture = home.Scene.Root.GetComponentInChildren<StoreBoxFurniture>();
        var collider = furniture.GetComponentsInChildren<Collider>().First(value => value.enabled);
        Vector2 delta = (Vector2)camera.transform.position - (Vector2)collider.bounds.center;
        home.Scene.MoveCamera(delta * (Screen.height / (2f * home.Scene.OrthographicSize * home.Scene.DragSensitivity)));
        Position = camera.WorldToScreenPoint(collider.bounds.center);
        Physics.SyncTransforms();
        if (!Physics.Raycast(camera.ScreenPointToRay(Position), out var hit, 1000f)
            || hit.collider.GetComponentInParent<StoreBoxFurniture>() != furniture)
            throw new InvalidOperationException("The original storage collider must be the nearest physical hit.");
        if (home.Scene.HitTest(hit.point) == (int)furniture.Furniture)
            throw new InvalidOperationException("The native storage collider must own the storage click.");
        return furniture;
    }

    public void Click(HomeFeatureRuntime home)
    {
        Center(home);
        var input = typeof(HomeFeatureRuntime).GetField("nativeSceneInput", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(home);
        var tick = input.GetType().GetMethod("Tick");
        var late = input.GetType().GetMethod("LateTick");
        LeftDown = true;
        tick.Invoke(input, null);
        LeftDown = false;
        late.Invoke(input, null);
        LeftUp = true;
        tick.Invoke(input, null);
        LeftUp = false;
        late.Invoke(input, null);
    }
}
