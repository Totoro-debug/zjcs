using UnityEngine;

namespace Zjcs.Presentation.Combat
{
    /// <summary>Projects unit-spaced cells in the local frame shared by the terrain and its units.</summary>
    public readonly struct PlanarGridProjection
    {
        private readonly Transform frame;
        private readonly Vector3 origin;

        public PlanarGridProjection(Transform frame, Vector3 origin)
        {
            this.frame = frame;
            this.origin = origin;
        }

        public Vector3 GridToWorld(float x, float y, float depthOffset = 0f) =>
            frame.TransformPoint(origin + new Vector3(x, y, depthOffset));

        public Vector2Int WorldToGrid(Vector3 world) => LocalToGrid(frame.InverseTransformPoint(world));

        public Vector2Int LocalToGrid(Vector3 local)
        {
            Vector3 offset = local - origin;
            // The original conversion adds half a cell and truncates toward zero.
            return new Vector2Int((int)(offset.x + .5f), (int)(offset.y + .5f));
        }

        public bool TryScreenToWorld(Camera camera, Vector2 screen, out Vector3 world)
        {
            world = default;
            if (camera == null) return false;
            Vector3 normal = Vector3.Cross(frame.TransformVector(Vector3.right), frame.TransformVector(Vector3.up));
            var plane = new Plane(normal, frame.TransformPoint(origin));
            Ray ray = camera.ScreenPointToRay(screen);
            if (!plane.Raycast(ray, out float distance)) return false;
            world = ray.GetPoint(distance);
            return true;
        }
    }
}
