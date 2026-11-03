using UnityEngine;

namespace Zjcs.Official.StoryPlayableManager
{
    public interface IBattleCameraOffsetHost
    {
        int RegisterCameraOffset(Vector3 offset, int group);
        void SetCameraOffset(int index, Vector3 offset);
        void UnregisterCameraOffset(int index);
        bool TryChangeFollowerWeight(Transform follower, float weight, out float previousWeight);
        bool AddExternalFollower(Transform follower, float weight);
        void RemoveExternalFollower(Transform follower);
    }

    public interface IBattleCameraPlayable
    {
        void BindCameraOffsetHost(IBattleCameraOffsetHost host);
    }

    public interface IBattleCharacterPartVisibilityHost
    {
        void SetCharacterPartVisibility(GameObject source, bool visible);
    }

    public interface IBattleCharacterPartVisibilityPlayable
    {
        void BindCharacterPartVisibilityHost(IBattleCharacterPartVisibilityHost host);
    }
}
