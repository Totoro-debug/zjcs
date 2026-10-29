// Rendering/engine adapters for executing the pinned ElementLinkGame methods.
// The generator inserts the original path and pointer method bodies unchanged.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
public class Vector2 {
 public float x,y; public Vector2(float a,float b){x=a;y=b;}
 public static Vector3 op_Implicit(Vector2 p){return new Vector3(p.x,p.y,0);}
}
public class Vector3 { public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;} }
public static class Time { public static float deltaTime=0.016f; }
public class PointerEventData { public Vector2 position;public PointerEventData(int x,int y){position=new Vector2(x,y);} }
public struct Pos2d {
 public int X,Y;public Pos2d(int x,int y){X=x;Y=y;}
 public static readonly Pos2d Down=new Pos2d(0,-1),Left=new Pos2d(-1,0),Up=new Pos2d(0,1),Right=new Pos2d(1,0);
 public static readonly List<Pos2d> FourDirs=new List<Pos2d>{Down,Left,Up,Right};
 public static Pos2d operator+(Pos2d a,Pos2d b){return new Pos2d(a.X+b.X,a.Y+b.Y);}
 public static Pos2d operator-(Pos2d a,Pos2d b){return new Pos2d(a.X-b.X,a.Y-b.Y);}
 public static bool operator==(Pos2d a,Pos2d b){return a.X==b.X&&a.Y==b.Y;}
 public static bool operator!=(Pos2d a,Pos2d b){return !(a==b);}
 public override bool Equals(object a){return a is Pos2d && this==(Pos2d)a;}
 public override int GetHashCode(){return X*397^Y;}
}
public class ElementStone { public int Type; }
public class ElementStoneBlock {
 public int Index,Type=-1,IsSelectedByType=-1;public Pos2d Pos2D;public ElementStone Stone;
 public bool IsHasElementStone {get{return Stone!=null;}}
 public bool IsActivate {get{return Type!=-1;}}
 public bool IsSelected {get{return IsSelectedByType!=-1;}}
 public bool TryGetElementStone(out ElementStone stone){stone=Stone;return Stone!=null;}
 public void SetSelected(int type,int index,bool isHasEffect=true,bool isHasAduio=true){Index=index;IsSelectedByType=type;}
 public void CancelSelected(bool isHasEffect=true,bool isHasAduio=true){Index=-1;IsSelectedByType=-1;}
 public void ActivateElementStoneBlock(bool isHasEffect=true,bool isHasAduio=true){if((!IsActivate||Type!=IsSelectedByType)&&IsSelectedByType!=-1)Type=IsSelectedByType;}
 public void DeactivateElementStoneBlock(bool isHasEffect=true,bool isHasAduio=true){if(IsActivate){Type=-1;CancelSelected(isHasEffect);}}
 public void SetIsLink(bool value){}
}
public class ElementLine {
 public bool linked;public Action<ElementLine,bool> OnLinkUpdated;
 public bool IsLink {get{return linked;}set{if(linked==value)return;linked=value;if(OnLinkUpdated!=null)OnLinkUpdated(this,value);}}
 public void AddPoint(Vector3 p){}public void Cutline(int i,bool isHasEffect=true){}public void ClearLine(bool isHasEffect=true){}public void Reverse(){}
}
public class ElementStoneBlockEffect {public void Activity(){} }
public class OriginalElementLink {
 private Dictionary<Pos2d,ElementStoneBlock> mElementStoneBlockDic=new Dictionary<Pos2d,ElementStoneBlock>();
 private Dictionary<int,ElementLine> mElementLineDic=new Dictionary<int,ElementLine>();
 private Dictionary<ElementLine,int> mElementLineTypeDic=new Dictionary<ElementLine,int>();
 private Dictionary<int,List<ElementStoneBlock>> mElementStoneBlockPaths=new Dictionary<int,List<ElementStoneBlock>>();
 private Dictionary<int,List<Pos2d>> mElementLinePaths=new Dictionary<int,List<Pos2d>>();
 private ElementStoneBlockEffect mElementStoneBlockEffect=new ElementStoneBlockEffect();
 private int mSelectedType=-1,mWightCellCount,mHightCellCount,mLinkSuccessCount;
 private Pos2d mPrePos2d=new Pos2d(-1,-1),mClikcPos2d,mDragStartPos2d;
 private List<Pos2d> mCurElementLinePath;
 private bool mGamePlaying=true,mIsDrag,mIsDraw,mIsDrawOne,mIsClickWillActivateDown;
 private float mDownTime;private readonly float mDownToDragTime=0;
 private Action mOnOperationEnd,mOnGameSuccess;
 private int operations;
 public OriginalElementLink(int width,int height,int[] grid){
  mWightCellCount=width;mHightCellCount=height;mOnOperationEnd=()=>operations++;
  for(int i=0;i<grid.Length;i++){
   int type=grid[i];Pos2d p=new Pos2d(i%width,i/width);
   mElementStoneBlockDic[p]=new ElementStoneBlock{Pos2D=p,Stone=type>=0?new ElementStone{Type=type}:null};
   if(type>=0&&!mElementLineDic.ContainsKey(type)){
    ElementLine line=new ElementLine();line.OnLinkUpdated=OnElementLineLinkUpdated;
    mElementLineDic[type]=line;mElementLineTypeDic[line]=type;mElementLinePaths[type]=new List<Pos2d>();mElementStoneBlockPaths[type]=new List<ElementStoneBlock>();
   }
  }
 }
 private Pos2d ColStoneBlockPosition(Vector2 p){return new Pos2d((int)p.x,(int)p.y);}
 private Vector2 StoneBlockPositionToLocalPosition(Pos2d p){return new Vector2(p.X,p.Y);}
 private void UpdateLineHead(int type){}
 // ORIGINAL_METHODS
 public object Step(string kind,int cell){
  PointerEventData data=new PointerEventData(cell<0?-1:cell%mWightCellCount,cell<0?-1:cell/mWightCellCount);
  switch(kind){case "down":OnPointerDown(data);break;case "move":OnPointerMove(data);break;case "up":OnPointerUp(data);break;case "exit":OnPointerExit(data);break;case "tick":Update();break;}
  Func<Pos2d,int> index=p=>p.X<0||p.Y<0?-1:p.Y*mWightCellCount+p.X;
  return new {
   paths=mElementLinePaths.ToDictionary(k=>k.Key.ToString(),v=>v.Value.Select(index).ToArray()),
   active=mElementStoneBlockPaths.ToDictionary(k=>k.Key.ToString(),v=>v.Value.Select(b=>index(b.Pos2D)).ToArray()),
   blocks=mElementStoneBlockDic.Values.Select(b=>new {type=b.Type,selected=b.IsSelectedByType,index=b.Index}).ToArray(),
   links=mElementLineDic.ToDictionary(k=>k.Key.ToString(),v=>v.Value.IsLink),
   selected=mSelectedType,previous=index(mPrePos2d),dragging=mIsDrag,drawing=mIsDraw,moved=mIsDrawOne,pending=mIsClickWillActivateDown,playing=mGamePlaying,operations=operations
  };
 }
}
public static class ElementLinkReference {
 public static string Run(string input){
  var output=new List<object>();
  foreach(JsonElement example in JsonDocument.Parse(input).RootElement.EnumerateArray()){
   var grid=example.GetProperty("grid").EnumerateArray().Select(v=>v.GetInt32()).ToArray();
   var game=new OriginalElementLink(example.GetProperty("width").GetInt32(),example.GetProperty("height").GetInt32(),grid);
   var snapshots=new List<object>();
   foreach(JsonElement action in example.GetProperty("actions").EnumerateArray())snapshots.Add(game.Step(action[0].GetString(),action[1].GetInt32()));
   output.Add(snapshots);
  }
  return JsonSerializer.Serialize(output);
 }
}
