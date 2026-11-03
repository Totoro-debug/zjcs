using UnityEngine;

internal static class BattleAfterimageRTUtils
{
    public static Matrix4x4 GetViewMatrix(Vector3 targetPosition)
    {
        Matrix4x4 result = Matrix4x4.identity;
        result.SetRow(0, new Vector4(1f, 0f, 0f, -targetPosition.x));
        result.SetRow(1, new Vector4(0f, 1f, 0f, -targetPosition.y));
        result.SetRow(2, new Vector4(0f, 0f, 1f, -100f));
        return result;
    }

    public static Matrix4x4 GetProjectionMatrix(int width, int height, float orthoSize)
    {
        float halfWidth = (float)width / height * orthoSize;
        return Matrix4x4.Ortho(-halfWidth, halfWidth, -orthoSize, orthoSize, .3f, 1000f);
    }
}
