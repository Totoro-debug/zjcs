using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zjcs.Presentation.Combat
{
    public interface IOfficialBattleNativeModel : IDisposable
    {
        Transform Transform { get; }
        Transform ModelTransform { get; }
        bool IsAnimationFinished { get; }
        bool HasAnimation(string name);
        bool Play(string name, bool loopAnimation, bool restart = false);
        void Tick(float deltaTime);
    }

    // Independent planar geometry adapter. All bones, weights, curves and polygons are source data;
    // Unity owns the native Transform hierarchy, Meshes, Materials and AnimationCurves.
    public sealed class OfficialSummonNativeModelView : IOfficialBattleNativeModel
    {
        private sealed class Draw
        { public GameObject Object; public Mesh Mesh; public MeshRenderer Renderer; }
        private struct ClipVertex
        {
            public Vector2 Position,UV;
            public ClipVertex(Vector2 position,Vector2 uv) { Position=position;UV=uv; }
        }
        private readonly OfficialSummonSkeletonSource source;
        private readonly Transform[] bones;
        private readonly Matrix4x4[] matrices;
        private readonly Draw[] draws;
        private readonly bool[] visibleDraws;
        private readonly float[] mixes;
        private readonly int[] bends,attachments,order;
        private readonly Color[] colors;
        private OfficialSummonAnimation animation;
        private float elapsed;
        private bool loop,disposed;
        public Transform Transform { get; }
        public Transform ModelTransform { get; }
        public bool IsAnimationFinished { get; private set; }
        public IReadOnlyList<Transform> Bones => bones;

        public OfficialSummonNativeModelView(OfficialSummonSkeletonData asset,Transform parent,string name)
        {
            if (!asset || !asset.Material || asset.Source?.bones == null)
                throw new InvalidOperationException("Original summon native data/material is not bound.");
            source=asset.Source;
            Transform=new GameObject(name).transform;Transform.SetParent(parent,false);
            var modelParent=Transform;
            foreach(var original in source.transforms)
            {
                var child=new GameObject(original.name).transform;child.SetParent(modelParent,false);
                child.localPosition=original.fields.m_LocalPosition;
                child.localRotation=original.fields.m_LocalRotation;
                child.localScale=original.fields.m_LocalScale;modelParent=child;
            }
            ModelTransform=modelParent;
            var group=modelParent.gameObject.AddComponent<SortingGroup>();group.sortingLayerID=0;
            group.sortingOrder=checked(source.sortingLayer*1000+source.sortingOrder);
            var skeletonRoot=new GameObject("NativeBones").transform;skeletonRoot.SetParent(modelParent,false);
            skeletonRoot.localScale=new Vector3(source.renderer.initialFlipX?-1:1,
                source.renderer.initialFlipY?-1:1,1);
            bones=new Transform[source.bones.Length];matrices=new Matrix4x4[bones.Length];
            sourceRotations=new float[bones.Length];
            for(int i=0;i<bones.Length;i++)
            {
                var b=source.bones[i];
                if(b.parent>=i) throw new InvalidOperationException("Original summon bone parent order differs.");
                bones[i]=new GameObject(b.name).transform;
                bones[i].SetParent(b.parent<0?skeletonRoot:bones[b.parent],false);
            }
            mixes=new float[source.constraints.Length];bends=new int[mixes.Length];
            attachments=new int[source.slots.Length];order=new int[attachments.Length];colors=new Color[attachments.Length];
            draws=new Draw[source.attachments.Length];
            visibleDraws=new bool[draws.Length];
            for(int i=0;i<draws.Length;i++)
            {
                if(source.attachments[i].kind=="clipping") continue;
                var go=new GameObject(source.attachments[i].name,typeof(MeshFilter),typeof(MeshRenderer));
                go.transform.SetParent(skeletonRoot,false);
                var mesh=new Mesh {name=source.attachments[i].name+" weighted geometry"};mesh.MarkDynamic();
                go.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.GetComponent<MeshRenderer>();
                renderer.sharedMaterial=asset.Material;renderer.enabled=false;
                draws[i]=new Draw {Object=go,Mesh=mesh,Renderer=renderer};
            }
            ApplyPose(null,0);
        }

        public bool HasAnimation(string name) => source.animations.Any(a=>a.name==name);
        public bool Play(string name,bool loopAnimation,bool restart=false)
        {
            if(disposed) throw new ObjectDisposedException(nameof(OfficialSummonNativeModelView));
            var next=source.animations.FirstOrDefault(a=>a.name==name);if(next==null)return false;
            if(animation==next && loop==loopAnimation && !restart)return true;
            animation=next;elapsed=0;loop=loopAnimation;IsAnimationFinished=false;ApplyPose(next,0);return true;
        }
        public void Tick(float deltaTime)
        {
            if(animation==null || disposed || IsAnimationFinished || deltaTime<=0)return;
            elapsed+=deltaTime;float time=elapsed;
            if(loop && animation.duration>0)time=Mathf.Repeat(time,animation.duration);
            else if(time>=animation.duration){time=animation.duration;IsAnimationFinished=true;}
            ApplyPose(animation,time);
        }

        public void ApplyPose(OfficialSummonAnimation pose,float time)
        {
            for(int i=0;i<bones.Length;i++)
            {
                var b=source.bones[i];bones[i].localPosition=new Vector3(b.x,b.y)*source.scale;
                bones[i].localRotation=Quaternion.Euler(0,0,b.rotation);
                bones[i].localScale=new Vector3(b.scaleX,b.scaleY,1);
            }
            for(int i=0;i<attachments.Length;i++)
            {
                attachments[i]=source.slots[i].attachment;order[i]=i;
                if(!ColorUtility.TryParseHtmlString("#"+source.slots[i].color,out colors[i]))
                    throw new InvalidOperationException("Original summon slot color is invalid.");
            }
            for(int i=0;i<mixes.Length;i++){mixes[i]=source.constraints[i].mix;bends[i]=source.constraints[i].bend;}
            if(pose!=null)
            {
                foreach(var track in pose.tracks)
                {
                    if(track.keys.Length==0 || time<track.keys[0].time)continue;
                    if(track.Curve==null)throw new InvalidOperationException("Original summon curve is not imported.");
                    float value=track.Curve.Evaluate(time);int i=track.index;
                    if(track.kind==0 && track.axis==2)
                        bones[i].localRotation=Quaternion.Euler(0,0,source.bones[i].rotation+value);
                    else if(track.kind==0)
                    {var p=bones[i].localPosition;p[track.axis]=(track.axis==0?source.bones[i].x:source.bones[i].y)*source.scale+value*source.scale;bones[i].localPosition=p;}
                    else if(track.kind==1)
                    {var s=bones[i].localScale;s[track.axis]=value*(track.axis==0?source.bones[i].scaleX:source.bones[i].scaleY);bones[i].localScale=s;}
                    else if(track.kind==2){var c=colors[i];c[track.axis]=value;colors[i]=c;}
                    else if(track.kind==3){if(track.axis==0)mixes[i]=value;else bends[i]=value<0?-1:1;}
                }
                foreach(var key in pose.attachments)if(key.time<=time)attachments[key.slot]=key.attachment;
                foreach(var key in pose.drawOrders)if(key.time<=time)Array.Copy(key.slots,order,order.Length);
            }
            for(int i=0;i<bones.Length;i++)sourceRotations[i]=bones[i].localEulerAngles.z;
            RefreshMatrices();
            foreach(int index in Enumerable.Range(0,mixes.Length).OrderBy(i=>source.constraints[i].order))
                SolveIk(index);
            DrawGeometry();
        }

        private void RefreshMatrices()
        {
            for(int i=0;i<bones.Length;i++)
            {
                var b=source.bones[i];var bone=bones[i];
                if(b.inherit=="noRotationOrReflection" && b.parent>=0)
                {
                    var p=matrices[b.parent];float sx=new Vector2(p.m00,p.m10).magnitude;
                    float sy=new Vector2(p.m01,p.m11).magnitude;
                    if(Mathf.Abs(sx-sy)>1e-4f || Mathf.Abs(p.m00*p.m01+p.m10*p.m11)>1e-4f || p.determinant<=0)
                        throw new InvalidOperationException("Restore nonuniform/reflected ancestor inheritance before playing this summon.");
                    float parentAngle=Mathf.Atan2(p.m10,p.m00)*Mathf.Rad2Deg;
                    // Local source rotation is stored separately from the counter-rotation required
                    // by Unity's ordinary parent hierarchy. Descendant matrices retain parent scale.
                    float local=sourceRotations==null?b.rotation:sourceRotations[i];
                    bone.localRotation=Quaternion.Euler(0,0,local-parentAngle);
                }
                var localMatrix=Matrix4x4.TRS(bone.localPosition,bone.localRotation,bone.localScale);
                matrices[i]=b.parent<0?localMatrix:matrices[b.parent]*localMatrix;
            }
        }
        private float[] sourceRotations;

        private void SolveIk(int index)
        {
            float mix=mixes[index];if(mix<=0)return;
            var c=source.constraints[index];var parent=bones[c.parent];int grand=source.bones[c.parent].parent;
            var grandMatrix=grand<0?Matrix4x4.identity:matrices[grand];
            Vector3 target=grandMatrix.inverse.MultiplyPoint3x4(matrices[c.target].MultiplyPoint3x4(Vector3.zero))-parent.localPosition;
            float angle=Mathf.Atan2(target.y,target.x)*Mathf.Rad2Deg;
            if(c.child<0)parent.localRotation=Quaternion.Euler(0,0,Mathf.LerpAngle(parent.localEulerAngles.z,angle,mix));
            else
            {
                var child=bones[c.child];var p=child.localPosition;
                if((parent.localScale-Vector3.one).sqrMagnitude>1e-8f || (child.localScale-Vector3.one).sqrMagnitude>1e-8f)
                    throw new InvalidOperationException("Restore scaled IK chain before playing this summon.");
                float l1=new Vector2(p.x,p.y).magnitude,l2=source.bones[c.child].length*source.scale;
                if(l1<=0 || l2<=0)throw new InvalidOperationException("Original summon IK length is invalid.");
                float cosine=Mathf.Clamp((target.sqrMagnitude-l1*l1-l2*l2)/(2*l1*l2),-1,1);
                float childAngle=Mathf.Atan2(p.y,p.x)+bends[index]*Mathf.Acos(cosine);
                float parentAngle=angle-Mathf.Atan2(p.y+l2*Mathf.Sin(childAngle),p.x+l2*Mathf.Cos(childAngle))*Mathf.Rad2Deg;
                parent.localRotation=Quaternion.Euler(0,0,Mathf.LerpAngle(parent.localEulerAngles.z,parentAngle,mix));
                child.localRotation=Quaternion.Euler(0,0,Mathf.LerpAngle(child.localEulerAngles.z,childAngle*Mathf.Rad2Deg,mix));
            }
            RefreshMatrices();
        }

        private Vector2[] Points(OfficialSummonAttachment attachment)
        {
            var points=new Vector2[attachment.vertices.Length];
            for(int i=0;i<points.Length;i++)
            {
                var vertex=attachment.vertices[i];Vector3 point=Vector3.zero;
                for(int j=0;j<vertex.count;j++)
                {
                    var w=attachment.influences[vertex.first+j];
                    point+=matrices[w.bone].MultiplyPoint3x4(new Vector3(w.x,w.y)*source.scale)*w.weight;
                }
                points[i]=point;
            }
            return points;
        }

        private void DrawGeometry()
        {
            Array.Clear(visibleDraws,0,visibleDraws.Length);
            Vector2[] clipping=null;int clipEnd=-1;
            for(int drawOrder=0;drawOrder<order.Length;drawOrder++)
            {
                int slot=order[drawOrder],id=attachments[slot];
                if(id>=0)
                {
                    var a=source.attachments[id];var points=Points(a);
                    if(a.kind=="clipping")
                    {if(source.renderer.useClipping){clipping=points;clipEnd=a.endSlot;}continue;}
                    var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
                    for(int i=0;i<a.triangles.Length;i+=3)
                    {
                        var polygon=new List<ClipVertex>();
                        for(int j=0;j<3;j++){int v=a.triangles[i+j];polygon.Add(new ClipVertex(points[v],new Vector2(a.uv[v*2],a.uv[v*2+1])));}
                        if(clipping!=null)polygon=Clip(polygon,clipping);
                        int first=vertices.Count;
                        foreach(var v in polygon){vertices.Add(new Vector3(v.Position.x,v.Position.y,slot*source.renderer.zSpacing));uv.Add(v.UV);}
                        for(int j=1;j<polygon.Count-1;j++){triangles.Add(first);triangles.Add(first+j);triangles.Add(first+j+1);}
                    }
                    var color=colors[slot];if(source.renderer.pmaVertexColors){color.r*=color.a;color.g*=color.a;color.b*=color.a;}
                    var draw=draws[id];draw.Mesh.Clear();draw.Mesh.SetVertices(vertices);draw.Mesh.SetUVs(0,uv);
                    draw.Mesh.SetColors(Enumerable.Repeat(color,vertices.Count).ToList());draw.Mesh.SetTriangles(triangles,0);draw.Mesh.RecalculateBounds();
                    draw.Renderer.sortingOrder=drawOrder;visibleDraws[id]=triangles.Count>0;
                }
                if(slot==clipEnd){clipping=null;clipEnd=-1;}
            }
            for(int i=0;i<draws.Length;i++)
                if(draws[i]!=null && draws[i].Renderer.enabled!=visibleDraws[i])draws[i].Renderer.enabled=visibleDraws[i];
        }
        private static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        private static List<ClipVertex> Clip(List<ClipVertex> polygon,Vector2[] boundary)
        {
            float area=0;for(int i=0;i<boundary.Length;i++)area+=Cross(boundary[i],boundary[(i+1)%boundary.Length]);
            if(Mathf.Abs(area)<1e-12f)return new List<ClipVertex>();
            float sign=area<0?-1:1;
            for(int i=0;i<boundary.Length;i++)
                if(sign*Cross(boundary[(i+1)%boundary.Length]-boundary[i],boundary[(i+2)%boundary.Length]-boundary[(i+1)%boundary.Length])<-.000001f)
                    throw new InvalidOperationException("Restore the original concave summon clipping polygon before rendering.");
            for(int i=0;i<boundary.Length && polygon.Count>0;i++)
            {
                Vector2 a=boundary[i],edge=boundary[(i+1)%boundary.Length]-a;
                var output=new List<ClipVertex>();var previous=polygon[polygon.Count-1];float pd=sign*Cross(edge,previous.Position-a);
                foreach(var current in polygon)
                {
                    float cd=sign*Cross(edge,current.Position-a);
                    if((pd>=0)!=(cd>=0))
                    {
                        float t=pd/(pd-cd);output.Add(new ClipVertex(Vector2.LerpUnclamped(previous.Position,current.Position,t),Vector2.LerpUnclamped(previous.UV,current.UV,t)));
                    }
                    if(cd>=0)output.Add(current);previous=current;pd=cd;
                }
                polygon=output;
            }
            return polygon;
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            foreach(var draw in draws)if(draw!=null)UnityEngine.Object.Destroy(draw.Mesh);
            if(Transform)UnityEngine.Object.Destroy(Transform.gameObject);
        }
    }
}
