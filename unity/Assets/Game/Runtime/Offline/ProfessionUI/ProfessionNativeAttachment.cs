using System;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.ProfessionUI
{
    public abstract class ProfessionNativeAttachment
    {
        public string Name { get; }
        public int SlotBone { get; }
        protected ProfessionNativeAttachment(string name,int slotBone)
        {
            if(name==null||slotBone<0)throw new ArgumentException("Original attachment name or slot bone is invalid.");
            Name=name;SlotBone=slotBone;
        }
    }

    // Original geometry stays in source units until its explicit data scale is
    // applied at the mesh/attachment API boundary. No pose is baked here.
    public sealed class ProfessionNativeRenderAttachment : ProfessionNativeAttachment
    {
        private readonly ProfessionNativeVertexAttachment vertices;
        private readonly Vector2[] localCorners,uvs;
        private readonly int[] triangles;
        private readonly float dataScale;
        public string Page { get; }
        public bool IsMesh => vertices!=null;
        public int VertexCount => vertices?.VertexCount??4;
        public int IndexCount => triangles.Length;

        public ProfessionNativeRenderAttachment(string name,string page,int slotBone,ProfessionNativeAtlasRegion region,
            float width,float height,float x,float y,float scaleX,float scaleY,float rotation,float dataScale)
            :base(name,slotBone)
        {
            Check(page,region,dataScale);Page=page;this.dataScale=dataScale;
            localCorners=region.LocalCorners(width,height,x,y,scaleX,scaleY,rotation);
            uvs=region.CornerUvs();triangles=new[]{0,1,2,2,3,0};
        }

        public ProfessionNativeRenderAttachment(string name,string page,int slotBone,int boneCount,ProfessionNativeAtlasRegion region,
            float[] sourceVertices,float[] sourceUvs,int[] sourceTriangles,float dataScale)
            :base(name,slotBone)
        {
            Check(page,region,dataScale);Page=page;this.dataScale=dataScale;
            uvs=region.MapMeshUvs(sourceUvs);
            vertices=new ProfessionNativeVertexAttachment(sourceVertices,uvs.Length,slotBone,boneCount);
            if(sourceTriangles==null||sourceTriangles.Length==0||sourceTriangles.Length%3!=0)throw new ArgumentException("Original mesh triangle list is invalid.");
            triangles=(int[])sourceTriangles.Clone();
            foreach(int i in triangles)if(i<0||i>=VertexCount)throw new ArgumentException("Original mesh triangle index is outside its vertex list.");
        }

        public Vector2 Uv(int vertex)=>uvs[vertex];
        public int Index(int index)=>triangles[index];
        public void ComputeVertices(ProfessionNativeBonePose pose,Vector3[] output)
        {
            if(pose==null||output==null||output.Length!=VertexCount||SlotBone>=pose.Count||!pose.IsActive(SlotBone))
                throw new ArgumentException("Original render attachment requires its active slot and matching output.");
            if(vertices!=null)
            {
                var world=new Vector2[VertexCount];vertices.Evaluate(pose,world);
                for(int i=0;i<world.Length;i++)output[i]=new Vector3(world[i].x*dataScale,world[i].y*dataScale,0);
            }
            else
            {
                Matrix4x4 world=pose.World(SlotBone);
                for(int i=0;i<4;i++)output[i]=world.MultiplyPoint3x4(localCorners[i])*dataScale;
            }
        }
        public void WriteMesh(ProfessionNativeBonePose pose,Mesh output,Color32 tint)
        {
            if(!output)throw new ArgumentNullException(nameof(output));
            var positions=new Vector3[VertexCount];ComputeVertices(pose,positions);
            var colors=new Color32[VertexCount];for(int i=0;i<colors.Length;i++)colors[i]=tint;
            output.Clear();output.name=Name;output.vertices=positions;output.uv=uvs;output.colors32=colors;output.triangles=triangles;output.RecalculateBounds();
        }
        private static void Check(string page,ProfessionNativeAtlasRegion region,float scale)
        {
            if(string.IsNullOrEmpty(page)||region==null||float.IsNaN(scale)||float.IsInfinity(scale)||scale<=0)
                throw new ArgumentException("Original render attachment atlas page or data scale is invalid.");
        }
    }

    public interface IProfessionNativeAttachmentSlot : IProfessionSkinClipSlot
    {
        ProfessionNativeBonePose Pose { get; }
    }

    public sealed class ProfessionNativeBoundingAttachment : ProfessionNativeAttachment,IProfessionSkinClipBoundingBox
    {
        private readonly ProfessionNativeVertexAttachment vertices;
        private readonly float dataScale;
        public int WorldVerticesLength=>vertices.VertexCount*2;
        public ProfessionNativeBoundingAttachment(string name,int slotBone,int boneCount,float[] sourceVertices,int vertexCount,float dataScale)
            :base(name,slotBone)
        {
            if(float.IsNaN(dataScale)||float.IsInfinity(dataScale)||dataScale<=0)throw new ArgumentException("Original bounding attachment data scale is invalid.");
            vertices=new ProfessionNativeVertexAttachment(sourceVertices,vertexCount,slotBone,boneCount);this.dataScale=dataScale;
        }
        public void ComputeWorldVertices(IProfessionSkinClipSlot slot,float[] output)
        {
            if(!(slot is IProfessionNativeAttachmentSlot native)||output==null||output.Length!=WorldVerticesLength)
                throw new ArgumentException("Original bounding attachment requires a native slot and matching output.");
            var world=new Vector2[vertices.VertexCount];vertices.Evaluate(native.Pose,world);
            for(int i=0;i<world.Length;i++){output[i*2]=world[i].x*dataScale;output[i*2+1]=world[i].y*dataScale;}
        }
    }

    public sealed class ProfessionNativeClippingAttachment : ProfessionNativeAttachment
    {
        private readonly ProfessionNativeVertexAttachment vertices;
        private readonly float dataScale;
        public int EndSlot { get; }
        public int VertexCount=>vertices.VertexCount;
        public ProfessionNativeClippingAttachment(string name,int slotBone,int boneCount,float[] sourceVertices,int vertexCount,int endSlot,int slotCount,float dataScale)
            :base(name,slotBone)
        {
            if(endSlot<0||endSlot>=slotCount||float.IsNaN(dataScale)||float.IsInfinity(dataScale)||dataScale<=0)
                throw new ArgumentException("Original clipping attachment end slot or data scale is invalid.");
            EndSlot=endSlot;vertices=new ProfessionNativeVertexAttachment(sourceVertices,vertexCount,slotBone,boneCount);this.dataScale=dataScale;
        }
        public void ComputeVertices(ProfessionNativeBonePose pose,Vector2[] output)
        {
            vertices.Evaluate(pose,output);for(int i=0;i<output.Length;i++)output[i]*=dataScale;
        }
    }

    public sealed class ProfessionNativePointAttachment : ProfessionNativeAttachment
    {
        private readonly Vector2 point;
        private readonly float dataScale;
        public ProfessionNativePointAttachment(string name,int slotBone,float x,float y,float dataScale):base(name,slotBone)
        {
            if(float.IsNaN(x)||float.IsInfinity(x)||float.IsNaN(y)||float.IsInfinity(y)||float.IsNaN(dataScale)||float.IsInfinity(dataScale)||dataScale<=0)
                throw new ArgumentException("Original point attachment coordinate or data scale is invalid.");
            point=new Vector2(x,y);this.dataScale=dataScale;
        }
        public Vector2 ComputePosition(ProfessionNativeBonePose pose)
        {
            if(pose==null||!pose.IsActive(SlotBone))throw new ArgumentException("Original point attachment requires an active native slot bone.");
            return (Vector2)pose.World(SlotBone).MultiplyPoint3x4(point)*dataScale;
        }
    }
}
