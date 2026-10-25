using System;
using System.Collections.Generic;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace Zjcs.Presentation.Opening
{
    /// <summary>AttachmentFollower.Initialize/Update/LateUpdate/GetPosZ from the official client.</summary>
    public sealed class OpeningAttachmentFollower : MonoBehaviour
    {
        private SkeletonAnimation renderer;
        private Bone bone;
        private Slot slot;
        private Attachment attachment;
        private GameObject[] targets;
        private bool followZ, zFront, followScale, followRotation, flip;
        public void Initialize(SkeletonAnimation skeleton,Dictionary<string,object> data,Transform[] nodes)
        {
            renderer=skeleton;
            bone=skeleton.Skeleton.FindBone(OpeningJson.String(data,"BoneName"));
            string slotName=OpeningJson.String(data,"SlotName"), attachmentName=OpeningJson.String(data,"AttachmentName");
            if (!string.IsNullOrEmpty(slotName) && !string.IsNullOrEmpty(attachmentName))
            {
                slot=skeleton.Skeleton.FindSlot(slotName);
                attachment=skeleton.Skeleton.GetAttachment(slotName,attachmentName);
                if (bone==null) bone=slot?.Bone;
            }
            if(bone==null) throw new InvalidOperationException("Official model follower bone missing: "+name);
            var ids=OpeningJson.Array(data["Gos"],"follower objects");
            targets=new GameObject[ids.Count];
            for(int i=0;i<ids.Count;i++)targets[i]=nodes[Convert.ToInt32(ids[i])].gameObject;
            // UnityPy preserves serialized MonoBehaviour bools as 0/1.
            followZ=NativeBool(data,"FollowZSpacing"); zFront=NativeBool(data,"ZFront");
            followScale=NativeBool(data,"FollowLocalScale"); followRotation=NativeBool(data,"FollowRotation");
            flip=NativeBool(data,"FollowSkeletonTransformFlipX");
            Update(); LateUpdate();
        }
        private static bool NativeBool(Dictionary<string,object> data,string key)
        { return OpeningJson.Bool(data,key) || OpeningJson.Number(data,key)!=0; }
        private void Update()
        {
            if (attachment==null) return;
            bool active=slot.Attachment==attachment;
            foreach(GameObject target in targets) if(target.activeSelf!=active)target.SetActive(active);
        }
        private void LateUpdate()
        {
            if (renderer==null || bone==null || !bone.Active || (attachment!=null && slot.Attachment!=attachment)) return;
            Vector3 point=new Vector3(bone.WorldX,bone.WorldY,0);
            if(followZ && attachment!=null)
            {
                int index=renderer.Skeleton.DrawOrder.IndexOf(slot);
                point.z=renderer.zSpacing*(index+(zFront?.5f:-.5f));
            }
            Transform source=renderer.transform;
            if(source==transform.parent)
            {
                transform.localPosition=point;
                if(followRotation)transform.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(bone.C,bone.A)*Mathf.Rad2Deg);
            }
            else
            {
                Vector3 position=source.TransformPoint(new Vector3(point.x,point.y,0));
                if(followZ && attachment!=null)position.z=source.position.z+point.z;
                transform.position=position;
                if(followRotation)
                {
                    float angle=bone.WorldRotationX;
                    if(source.lossyScale.x*source.lossyScale.y<0)angle=-angle;
                    Vector3 euler=source.rotation.eulerAngles;
                    transform.rotation=Quaternion.Euler(euler.x,euler.y,euler.z+angle);
                }
            }
            if(followScale || flip)
            {
                Vector3 scale=transform.localScale;
                if(followScale){scale.x=bone.ScaleX;scale.y=bone.ScaleY;}
                if(flip)scale.x*=source.lossyScale.x>0?1:-1;
                transform.localScale=scale;
            }
        }
    }
}
