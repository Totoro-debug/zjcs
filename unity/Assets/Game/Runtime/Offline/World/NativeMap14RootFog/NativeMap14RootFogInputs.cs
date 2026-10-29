using System;
using System.Reflection;
using UnityEngine;
using Zjcs.Official.MapRootFog;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Offline.World.NativeMap14SharedEntities;
namespace Zjcs.Offline.World.NativeMap14RootFog
{
 public sealed class NativeMap14RootFogInputs
 {
  public FogGroundData Ground {get;} public FogVisibilityData Visibility {get;}
  public NativeMap14RootFogInputs(NativeMap14SharedAssemblies shared,object root)
  {
   if(shared==null||root==null)throw new ArgumentNullException();
   var get=root.GetType().GetMethod("GetComponent",new[]{typeof(Type)});
   Ground=new GroundInput(get.Invoke(root,new object[]{shared.CommonType("GridGroundComponent")}));
   Visibility=new VisibilityInput(get.Invoke(root,new object[]{shared.CommonType("GridVisibilityAgentComponent")}));
  }
  private static object Get(object value,string name)=>NativeMap14SharedPrefabs.Get(value,name);
  private static Pos2d Position(object value)=>new Pos2d((int)Get(value,"X"),(int)Get(value,"Y"));
  private sealed class GroundInfo:FogGroundInfo
  {
   private readonly object ground;public GroundInfo(object value){ground=value;}
   private object Info=>Get(ground,"_Info");
   public override Pos2d Size=>Position(Get(Info,"Size"));
   public override Vector3 OriginPos=>(Vector3)Get(Info,"OriginPos");
  }
  private sealed class GroundInput:FogGroundData
  {
   public override object Original {get;} public override FogGroundInfo Info {get;}
   private readonly MethodInfo grid,position,toGrid;
   public GroundInput(object value)
   {
    Original=value??throw new ArgumentException("Original root ground missing");Info=new GroundInfo(value);
    grid=value.GetType().GetMethod("GridToPos",new[]{typeof(float),typeof(float)});
    position=Array.Find(value.GetType().GetMethods(),m=>m.Name=="GridToPos"&&m.GetParameters().Length==1);
    toGrid=value.GetType().GetMethod("PosToGrid",new[]{typeof(Vector3)});
   }
   public override Vector3 GridToPos(float x,float y)=>(Vector3)grid.Invoke(Original,new object[]{x,y});
   public override Vector3 GridToPos(Pos2d pos)=>(Vector3)position.Invoke(Original,new[]{Activator.CreateInstance(position.GetParameters()[0].ParameterType,pos.X,pos.Y)});
   public override Pos2d PosToGrid(Vector3 pos)=>Position(toGrid.Invoke(Original,new object[]{pos}));
  }
  private sealed class VisibilityInput:FogVisibilityData
  {
   public override object Original {get;}private readonly MethodInfo visible;
   public VisibilityInput(object value){Original=value??throw new ArgumentException("Original root visibility missing");visible=value.GetType().GetMethod("GetPosVisibility",new[]{typeof(int),typeof(int)});}
   public override bool GetPosVisibility(int x,int y)=>(bool)visible.Invoke(Original,new object[]{x,y});
  }
 }
}
