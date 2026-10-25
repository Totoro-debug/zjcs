using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace Zjcs.Presentation.Opening
{
    /// <summary>DamageSequenceImage.OnPopulateMesh from the official client.</summary>
    public sealed class OpeningDamageSequenceImage : Image, Zjcs.Presentation.Combat.IBattleHudSequenceImage
    {
        private int rows=1,columns=1;
        private float rate;
        public void Initialize(int row,int column,float value) {rows=row;columns=column;Rate=value;}
        public float Rate {get=>rate;set {rate=value;SetVerticesDirty();}}
        protected override void OnPopulateMesh(VertexHelper fill)
        {
            fill.Clear();
            if(overrideSprite==null)return;
            Vector4 uv=DataUtility.GetInnerUV(overrideSprite);
            float width=(uv.z-uv.x)/columns,height=(uv.w-uv.y)/rows;
            float index=rate*(rows*columns-1);
            int row=Mathf.FloorToInt(index/columns),column=(int)(index-row*columns);
            Vector2 min=new Vector2(column*width+uv.x,uv.w-(row+1)*height+uv.y);
            Vector2 max=new Vector2((column+1)*width+uv.x,uv.w-row*height+uv.y);
            Rect rect=GetPixelAdjustedRect();
            fill.AddVert(new Vector3(rect.xMin,rect.yMin),color,new Vector2(min.x,min.y));
            fill.AddVert(new Vector3(rect.xMin,rect.yMax),color,new Vector2(min.x,max.y));
            fill.AddVert(new Vector3(rect.xMax,rect.yMax),color,new Vector2(max.x,max.y));
            fill.AddVert(new Vector3(rect.xMax,rect.yMin),color,new Vector2(max.x,min.y));
            fill.AddTriangle(0,1,2);fill.AddTriangle(2,3,0);
        }
    }
}
