using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Offline.ProfessionUI
{
    // Independent half-plane intersection. The six original character clip
    // attachments are convex quadrilaterals. Concave input fails explicitly.
    public sealed class ProfessionNativeConvexClipper
    {
        private struct Vertex
        {
            public Vector2 Point,Uv;
            public Vertex(Vector2 point,Vector2 uv){Point=point;Uv=uv;}
        }
        private readonly Vector2[] boundary;
        private readonly double sign;
        private readonly List<Vertex> first=new List<Vertex>(),second=new List<Vertex>();
        public bool HasArea=>sign!=0;

        public ProfessionNativeConvexClipper(Vector2[] polygon)
        {
            if(polygon==null||polygon.Length<3)throw new ArgumentException("Original clipping polygon has fewer than three vertices.");
            boundary=(Vector2[])polygon.Clone();double area=0;
            for(int i=0;i<boundary.Length;i++)
            {
                Finite(boundary[i]);Vector2 next=boundary[(i+1)%boundary.Length];
                area+=(double)boundary[i].x*next.y-(double)next.x*boundary[i].y;
            }
            sign=Math.Sign(area);
            if(sign==0)return;
            for(int i=0;i<boundary.Length;i++)
                if(sign*Cross(boundary[i],boundary[(i+1)%boundary.Length],boundary[(i+2)%boundary.Length])<0)
                    throw new NotSupportedException("This clipping polygon requires independently verified concave decomposition.");
        }

        public void AppendTriangle(Vector2 a,Vector2 b,Vector2 c,Vector2 uvA,Vector2 uvB,Vector2 uvC,
            List<Vector3> vertices,List<Vector2> uvs,List<int> indices)
        {
            if(vertices==null||uvs==null||indices==null||vertices.Count!=uvs.Count)
                throw new ArgumentException("Original clipped mesh output lists differ.");
            Finite(a);Finite(b);Finite(c);Finite(uvA);Finite(uvB);Finite(uvC);
            if(!HasArea||Cross(a,b,c)==0)return;
            first.Clear();second.Clear();first.Add(new Vertex(a,uvA));first.Add(new Vertex(b,uvB));first.Add(new Vertex(c,uvC));
            List<Vertex> input=first,output=second;
            for(int edge=0;edge<boundary.Length&&input.Count>0;edge++)
            {
                output.Clear();Vector2 p=boundary[edge],q=boundary[(edge+1)%boundary.Length];
                Vertex previous=input[input.Count-1];double previousDistance=sign*Cross(p,q,previous.Point);
                foreach(Vertex current in input)
                {
                    double distance=sign*Cross(p,q,current.Point);
                    if((previousDistance>=0)!=(distance>=0))
                    {
                        double t=previousDistance/(previousDistance-distance);
                        output.Add(new Vertex(Interpolate(previous.Point,current.Point,t),Interpolate(previous.Uv,current.Uv,t)));
                    }
                    if(distance>=0)output.Add(current);
                    previous=current;previousDistance=distance;
                }
                List<Vertex> swap=input;input=output;output=swap;
            }
            for(int i=1;i+1<input.Count;i++)
            {
                Vertex x=input[0],y=input[i],z=input[i+1];
                if(Cross(x.Point,y.Point,z.Point)==0)continue;
                Append(x,vertices,uvs,indices);Append(y,vertices,uvs,indices);Append(z,vertices,uvs,indices);
            }
        }
        private static void Append(Vertex v,List<Vector3> vertices,List<Vector2> uvs,List<int> indices)
        {indices.Add(vertices.Count);vertices.Add(v.Point);uvs.Add(v.Uv);}
        private static Vector2 Interpolate(Vector2 a,Vector2 b,double t)
        {return new Vector2((float)(a.x+((double)b.x-a.x)*t),(float)(a.y+((double)b.y-a.y)*t));}
        private static double Cross(Vector2 a,Vector2 b,Vector2 c)
        {return ((double)b.x-a.x)*((double)c.y-a.y)-((double)b.y-a.y)*((double)c.x-a.x);}
        private static void Finite(Vector2 v)
        {if(float.IsNaN(v.x)||float.IsInfinity(v.x)||float.IsNaN(v.y)||float.IsInfinity(v.y))throw new ArgumentException("Original clipping coordinates must be finite.");}
    }
}
