using System;
using UnityEngine;

namespace Zjcs.Presentation.Combat
{
    // Native Unity geometry and curves, decoded from official data without a Spine runtime.
    public sealed class OfficialSummonSkeletonData : ScriptableObject
    {
        public OfficialSummonSkeletonSource Source;
        public Material Material;
        public Sprite Portrait;
    }

    [Serializable] public sealed class OfficialSummonSkeletonSource
    {
        public int id, modelId;
        public int sortingLayer,sortingOrder;
        public bool showOnRoundProgress;
        public float scale, defaultMix;
        public OfficialSummonBone[] bones;
        public OfficialSummonSlot[] slots;
        public OfficialSummonIk[] constraints;
        public OfficialSummonAttachment[] attachments;
        public OfficialSummonAnimation[] animations;
        public OfficialSummonTransform[] transforms;
        public OfficialSummonRenderer renderer;
    }
    [Serializable] public sealed class OfficialSummonBone
    { public string name,inherit; public int parent; public float x,y,rotation,scaleX=1,scaleY=1,length; }
    [Serializable] public sealed class OfficialSummonSlot
    { public string name,color,blend; public int bone,attachment; }
    [Serializable] public sealed class OfficialSummonIk
    { public string name; public int parent,child,target,order,bend; public float mix; }
    [Serializable] public sealed class OfficialSummonAttachment
    {
        public string name,kind;
        public int slot,endSlot;
        public OfficialSummonVertex[] vertices;
        public OfficialSummonInfluence[] influences;
        public float[] uv;
        public int[] triangles;
    }
    [Serializable] public sealed class OfficialSummonVertex { public int first,count; }
    [Serializable] public sealed class OfficialSummonInfluence { public int bone; public float x,y,weight; }
    [Serializable] public sealed class OfficialSummonAnimation
    {
        public string name;
        public float duration;
        public OfficialSummonTrack[] tracks;
        public OfficialSummonAttachmentKey[] attachments;
        public OfficialSummonDrawOrder[] drawOrders;
    }
    [Serializable] public sealed class OfficialSummonTrack
    {
        // kind: 0 translation/rotation, 1 scale, 2 slot color, 3 IK mix/bend.
        public int kind,index,axis;
        public OfficialSummonCurveKey[] keys;
        public AnimationCurve Curve;
    }
    [Serializable] public sealed class OfficialSummonCurveKey
    { public float time,value; public bool stepped; public float[] curve; }
    [Serializable] public sealed class OfficialSummonAttachmentKey
    { public int slot,attachment; public float time; }
    [Serializable] public sealed class OfficialSummonDrawOrder { public float time; public int[] slots; }
    [Serializable] public sealed class OfficialSummonTransform
    { public string name; public OfficialSummonTransformFields fields; }
    [Serializable] public sealed class OfficialSummonTransformFields
    { public Vector3 m_LocalPosition,m_LocalScale; public Quaternion m_LocalRotation; }
    [Serializable] public sealed class OfficialSummonRenderer
    {
        public bool initialFlipX,initialFlipY,pmaVertexColors,tintBlack,useClipping;
        public float zSpacing;
        public string initialSkinName;
    }
}
