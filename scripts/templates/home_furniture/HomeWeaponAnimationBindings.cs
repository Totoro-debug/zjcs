using Spine.Unity;

namespace Zjcs.Official.HomeFurniture
{
    public interface IHomeWeaponAnimationBindings
    {
        Spine.Animation GetOrLoadSpineAnimation(ISkeletonComponent component, string animationName);
        void UpdateAnimation(SkeletonRenderer renderer);
        void UpdateAnimation(SkeletonGraphic graphic);
    }
}
