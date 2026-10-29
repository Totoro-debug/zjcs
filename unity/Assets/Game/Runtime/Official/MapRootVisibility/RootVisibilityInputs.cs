using System;
using System.Collections.Generic;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.MapRootFog;
using Zjcs.Official.World.NativeTerrain.Common;
namespace Zjcs.Official.MapRootVisibility
{
 public interface IFogVisibilityPlayer
 {
  FogCharacterData Character {get;} MyEntity GetEntity(EcEntityIdentifier id);
  IFogTransferClient GetTransferClient(MyEntity entity);Pos2d? MyMeUiGridPos {get;}
 }
 public abstract class FogCharacterData {public abstract EcEntityIdentifier FindWorldTransferAction();public abstract Pos2d GridPos {get;}}
 public interface IFogTransferClient {FogTransferData Component {get;}}
 public abstract class FogTransferData {public abstract Pos2d Pos {get;}}
 public abstract class FogMultiCellHandle
 {
  public abstract event Action<HashSet<Pos2d>,bool> OnMultiCellsUnlock;
 }
 public abstract class FogVisibilityAgentData:FogVisibilityData,IEntityComponentInfoSource,IDisposable
 {
  public abstract override object Original {get;}public abstract object ComponentInfo {get;}
  public abstract T GetComponent<T>() where T:class;
  public abstract FogMultiCellHandle Handle {get;}
  public abstract event Action<Pos2d,bool,bool,bool> OnPosVisibilityUpdate;
  public abstract event Action<Pos2d,bool> OnOneCellUnlock;
  public abstract void Dispose();
 }
}
