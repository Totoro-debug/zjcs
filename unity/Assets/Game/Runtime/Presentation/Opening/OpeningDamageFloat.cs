using UnityEngine;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Opening
{
    /// <summary>CharacterDamageUI lifetime/attachment with its original animation curves.</summary>
    public sealed class OpeningDamageFloat : MonoBehaviour
    {
        private OfficialBattleHudAnimationPlayer animations;
        private RectTransform parent, rect;
        private Camera camera;
        private Vector3 anchor;
        private float elapsed, duration;
        public int TargetId { get; private set; }
        public Vector3 StackOffset { get; set; }

        public void Initialize(OfficialBattleHudAnimationPlayer player, float showTime,
            RectTransform screenParent, Camera worldCamera, Vector3 worldAnchor, int targetId)
        {
            animations=player; duration=showTime; parent=screenParent; camera=worldCamera;
            anchor=worldAnchor; TargetId=targetId; rect=(RectTransform)transform;
            Project();
        }
        private void Update()
        {
            animations.Tick(Time.deltaTime);
            elapsed+=Time.deltaTime;
            Project();
            if (elapsed>=duration) Destroy(gameObject);
        }
        private void Project()
        {
            Vector3 screen=camera.WorldToScreenPoint(anchor+StackOffset);
            if (screen.z>=0 && RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,screen,null,out Vector2 local))
                rect.localPosition=new Vector3(local.x,local.y,rect.localPosition.z);
        }
    }
}
