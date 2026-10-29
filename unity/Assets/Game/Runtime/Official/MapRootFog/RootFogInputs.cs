using UnityEngine;
using Zjcs.Official.World.NativeTerrain.Common;
namespace Zjcs.Official.MapRootFog
{
 public interface IRootFogPlatform {Camera MapCamera {get;} bool IsMapScene {get;}}
 public abstract class FogGroundInfo {public abstract Pos2d Size {get;} public abstract Vector3 OriginPos {get;}}
 public abstract class FogGroundData
 {
  public abstract object Original {get;} public abstract FogGroundInfo Info {get;}
  public abstract Vector3 GridToPos(float x,float y);
  public abstract Vector3 GridToPos(Pos2d pos);
  public abstract Pos2d PosToGrid(Vector3 pos);
 }
 public abstract class FogVisibilityData {public abstract object Original {get;} public abstract bool GetPosVisibility(int x,int y);}
}
