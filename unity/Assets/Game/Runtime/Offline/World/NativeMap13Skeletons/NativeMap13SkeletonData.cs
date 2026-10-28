using System;
using UnityEngine;

namespace Zjcs.Offline.World.NativeMap13Skeletons
{
    // Native Unity geometry and curves, decoded from official data without a Spine runtime.
    public sealed class NativeMap13SkeletonData : ScriptableObject
    {
        public NativeMap13SkeletonSource Source;
        public Material Material;

    }

    [Serializable] public sealed class NativeMap13SkeletonSource
    {
        public string name, cab, path_id, materialPath;
        public int sortingLayer,sortingOrder;
        public bool showOnRoundProgress;
        public float scale, defaultMix;
        public NativeMap13Bone[] bones;
        public NativeMap13Slot[] slots;
        public NativeMap13Ik[] constraints;
        public NativeMap13Attachment[] attachments;
        public NativeMap13Animation[] animations;
        public NativeMap13Transform[] transforms;
        public NativeMap13Renderer renderer;
    }
    [Serializable] public sealed class NativeMap13Bone
    { public string name,inherit; public int parent; public float x,y,rotation,shearX,shearY,scaleX=1,scaleY=1,length; }
    [Serializable] public sealed class NativeMap13Slot
    { public string name,color,blend; public int bone,attachment; }
    [Serializable] public sealed class NativeMap13Ik
    { public string name; public int parent,child,target,order,bend; public float mix; }
    [Serializable] public sealed class NativeMap13Attachment
    {
        public string name,kind;
        public int slot,endSlot;
        public NativeMap13Vertex[] vertices;
        public NativeMap13Influence[] influences;
        public float[] uv;
        public int[] triangles;
    }
    [Serializable] public sealed class NativeMap13Vertex { public int first,count; }
    [Serializable] public sealed class NativeMap13Influence { public int bone; public float x,y,weight; }
    [Serializable] public sealed class NativeMap13Animation
    {
        public string name;
        public float duration;
        public NativeMap13Track[] tracks;
        public NativeMap13AttachmentKey[] attachments;
        public NativeMap13DrawOrder[] drawOrders;
    }
    [Serializable] public sealed class NativeMap13Track
    {
        // kind: 0 translation/rotation, 1 scale, 2 slot color, 3 IK mix/bend, 4 shear.
        public int kind,index,axis;
        public NativeMap13CurveKey[] keys;
        public AnimationCurve Curve;
    }
    [Serializable] public sealed class NativeMap13CurveKey
    { public float time,value; public bool stepped; public float[] curve; }
    [Serializable] public sealed class NativeMap13AttachmentKey
    { public int slot,attachment; public float time; }
    [Serializable] public sealed class NativeMap13DrawOrder { public float time; public int[] slots; }
    [Serializable] public sealed class NativeMap13Transform
    { public string name; public NativeMap13TransformFields fields; }
    [Serializable] public sealed class NativeMap13TransformFields
    { public Vector3 m_LocalPosition,m_LocalScale; public Quaternion m_LocalRotation; }
    [Serializable] public sealed class NativeMap13Renderer
    {
        public bool initialFlipX,initialFlipY,pmaVertexColors,tintBlack,useClipping;
        public float zSpacing;
        public string initialSkinName;
        public NativeMap13Translator translator;
    }
    [Serializable] public sealed class NativeMap13Translator
    { public bool autoReset,useCustomMixMode; public int[] layerMixModes,layerBlendModes; }
}
