using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace Zjcs.Offline.ProfessionUI
{
    // Geometry only. The original appearance/UI service remains responsible
    // for page materials, separator placement and CanvasRenderer assignment.
    public sealed class ProfessionNativeMeshBatches : IDisposable
    {
        public sealed class Slot
        {
            public readonly int Index,Blend;
            public readonly bool Active,BreakAfter;
            public readonly Color32 Color;
            public readonly ProfessionNativeAttachment Attachment;
            public Slot(int index,ProfessionNativeAttachment attachment,Color32 color,int blend=0,bool active=true,bool breakAfter=false)
            {Index=index;Attachment=attachment;Color=color;Blend=blend;Active=active;BreakAfter=breakAfter;}
        }
        public sealed class Batch
        {
            public Mesh Mesh {get;}
            public string Page {get;internal set;}
            public int Blend {get;internal set;}
            public int FirstDrawIndex {get;internal set;}
            public int LastDrawIndex {get;internal set;}
            internal readonly List<Vector3> Vertices=new List<Vector3>();
            internal readonly List<Vector2> Uvs=new List<Vector2>();
            internal readonly List<Color32> Colors=new List<Color32>();
            internal readonly List<int> Indices=new List<int>();
            internal Batch(){Mesh=new Mesh{name="Original character native draw batch"};Mesh.MarkDynamic();}
            internal void Clear(){Vertices.Clear();Uvs.Clear();Colors.Clear();Indices.Clear();}
        }
        private readonly int slotCount;
        private readonly List<Batch> pool=new List<Batch>(),active=new List<Batch>();
        private readonly List<Vector3> clippedVertices=new List<Vector3>();
        private readonly List<Vector2> clippedUvs=new List<Vector2>();
        private readonly List<int> clippedIndices=new List<int>();
        private readonly bool[] seen;
        private bool disposed;
        public ReadOnlyCollection<Batch> Batches {get;}
        public ProfessionNativeMeshBatches(int slotCount)
        {
            if(slotCount<=0)throw new ArgumentOutOfRangeException(nameof(slotCount));
            this.slotCount=slotCount;seen=new bool[slotCount];Batches=active.AsReadOnly();
        }
        public void Build(ProfessionNativeBonePose pose,IReadOnlyList<Slot> drawOrder,bool useClipping=true,bool pmaVertexColors=false)
        {
            if(disposed)throw new ObjectDisposedException(nameof(ProfessionNativeMeshBatches));
            if(pose==null||drawOrder==null||drawOrder.Count!=slotCount)throw new ArgumentException("Original draw order must include every source slot.");
            Array.Clear(seen,0,seen.Length);
            foreach(Slot slot in drawOrder)
            {
                if(slot==null||slot.Index<0||slot.Index>=slotCount||seen[slot.Index]||slot.Blend<0||slot.Blend>3||slot.Attachment!=null&&slot.Attachment.SlotBone>=pose.Count)
                    throw new ArgumentException("Original draw order is not a valid source-slot permutation.");
                seen[slot.Index]=true;
            }
            active.Clear();Batch current=null;ProfessionNativeConvexClipper clipping=null;int clipEnd=-1,clipStart=-1;
            for(int draw=0;draw<drawOrder.Count;draw++)
            {
                Slot slot=drawOrder[draw];ProfessionNativeAttachment attachment=slot.Attachment;
                if(slot.Active&&attachment!=null&&pose.IsActive(attachment.SlotBone))
                {
                    if(useClipping&&attachment is ProfessionNativeClippingAttachment clip)
                    {
                        if(clipping!=null)throw new NotSupportedException("Original clipping ranges overlap; their draw-order binding must be restored.");
                        var boundary=new Vector2[clip.VertexCount];clip.ComputeVertices(pose,boundary);clipping=new ProfessionNativeConvexClipper(boundary);clipEnd=clip.EndSlot;clipStart=slot.Index;
                    }
                    else if(attachment is ProfessionNativeRenderAttachment render)
                    {
                        var points=new Vector3[render.VertexCount];render.ComputeVertices(pose,points);
                        clippedVertices.Clear();clippedUvs.Clear();clippedIndices.Clear();
                        if(clipping==null)
                        {
                            clippedVertices.AddRange(points);
                            for(int i=0;i<points.Length;i++)clippedUvs.Add(render.Uv(i));
                            for(int i=0;i<render.IndexCount;i++)clippedIndices.Add(render.Index(i));
                        }
                        else
                        {
                            for(int i=0;i<render.IndexCount;i+=3)
                            {
                                int a=render.Index(i),b=render.Index(i+1),c=render.Index(i+2);
                                clipping.AppendTriangle(points[a],points[b],points[c],render.Uv(a),render.Uv(b),render.Uv(c),clippedVertices,clippedUvs,clippedIndices);
                            }
                        }
                        if(clippedIndices.Count>0)
                        {
                            if(current==null||current.Page!=render.Page||current.Blend!=slot.Blend)
                            {
                                if(active.Count==pool.Count)pool.Add(new Batch());current=pool[active.Count];current.Clear();current.Page=render.Page;current.Blend=slot.Blend;current.FirstDrawIndex=draw;active.Add(current);
                            }
                            current.LastDrawIndex=draw;int offset=current.Vertices.Count;current.Vertices.AddRange(clippedVertices);current.Uvs.AddRange(clippedUvs);
                            Color32 color=slot.Color;
                            if(pmaVertexColors)color=new Color32((byte)((color.r*color.a+127)/255),(byte)((color.g*color.a+127)/255),(byte)((color.b*color.a+127)/255),color.a);
                            for(int i=0;i<clippedVertices.Count;i++)current.Colors.Add(color);
                            foreach(int i in clippedIndices)current.Indices.Add(offset+i);
                        }
                    }
                }
                // End is inclusive, including an invisible/empty end slot.
                // An end equal to the clipping slot means end of draw order.
                if(clipping!=null&&slot.Index==clipEnd&&slot.Index!=clipStart){clipping=null;clipEnd=clipStart=-1;}
                if(slot.BreakAfter)current=null;
            }
            foreach(Batch batch in active)
            {
                Mesh mesh=batch.Mesh;mesh.Clear();mesh.indexFormat=batch.Vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16;
                mesh.SetVertices(batch.Vertices);mesh.SetUVs(0,batch.Uvs);mesh.SetColors(batch.Colors);mesh.SetTriangles(batch.Indices,0,true);mesh.RecalculateBounds();
            }
            for(int i=active.Count;i<pool.Count;i++)pool[i].Mesh.Clear();
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            foreach(Batch batch in pool)if(batch.Mesh){if(Application.isPlaying)Object.Destroy(batch.Mesh);else Object.DestroyImmediate(batch.Mesh);}
            active.Clear();pool.Clear();clippedVertices.Clear();clippedUvs.Clear();clippedIndices.Clear();
        }
    }
}
