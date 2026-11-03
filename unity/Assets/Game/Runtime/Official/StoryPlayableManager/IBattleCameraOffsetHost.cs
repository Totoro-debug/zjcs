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

    public interface IBattleAfterimageHost
    {
        UnityEngine.RenderTexture GetRT(int width, int height);
        void ReturnRT(UnityEngine.RenderTexture texture);
    }

    public interface IBattleAfterimagePlayable
    {
        void BindAfterimageHost(IBattleAfterimageHost host);
    }

    public interface IBattleCameraPerformanceHost
    {
        long BlockGameObjectInput();
        void UnblockGameObjectInput(long operationId);
        void SwitchInPerformanceCamera();
        void SwitchOutPerformanceCamera();
    }

    public interface IBattleCameraPerformancePlayable
    {
        void BindCameraPerformanceHost(IBattleCameraPerformanceHost host);
    }

    public interface IBattleMasterEntityResolver
    {
        bool TryGetMasterEntity(GameObject character, out GameObject master);
    }

    public interface IBattleMasterEntityOverridePlayable
    {
        void BindMasterEntityResolver(IBattleMasterEntityResolver resolver);
    }
}
