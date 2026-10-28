// Original ECGridTransformView. ECGridTransformComponent and MoveAgent access
// are the explicit service boundary; formulas and operation order are retained.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.SteleEntities
{
    [Serializable]
    public sealed class ECGridTransformView : ECViewBase
    {
        [SerializeField] private bool mUpdatePosImmediately;
        [SerializeField] private bool mSetZ;
        [SerializeField] private float mOffsetZ;
        [SerializeField] private float mOffsetZPerX = 0.001f;
        [SerializeField] private Transform mFlipTransform;
        [SerializeField] private List<Pos2d> mClientOverrideEntityDimensionBounds;
        [NonSerialized] private Transform mTransform;
        public bool UpdatePosImmediately => mUpdatePosImmediately;
        public bool SetZ => mSetZ;
        public float OffsetZ => mOffsetZ;
        public Transform FlipTransform => mFlipTransform;
        public List<Pos2d> ClientOverrideEntityDimensionBounds => mClientOverrideEntityDimensionBounds;
        public event Action<ECGridTransformView, Pos2d> OnPosUpdated;
        protected override void Load()
        {
            mTransform = mEntity.EntityObject.transform;
            mEntity.GridPositionChanged += GridTransform_OnPosUpdated;
        }
        protected override void Unload() => mEntity.GridPositionChanged -= GridTransform_OnPosUpdated;
        private void GridTransform_OnPosUpdated(Pos2d previous)
        {
            if (UpdatePosImmediately)
            {
                if (mEntity.IsMoving) mEntity.StopMovement();
                SetPositionByGrid(mEntity.Grid);
            }
            try { OnPosUpdated?.Invoke(this, previous); } catch (Exception error) { Debug.LogException(error, mOwner); }
        }
        public Vector3 GridToPosition(Pos2d grid) => mEntity.GridToPosition(grid);
        public Pos2d PositionToGrid(Vector3 position) => mEntity.PositionToGrid(position);
        public void SetPositionByGrid(Pos2d grid) => SetPosition(GridToPosition(grid));
        public void SetScaleByFlipX(bool flipX)
        {
            if (mFlipTransform != null)
            {
                Vector3 localScale = mFlipTransform.localScale;
                if (flipX == (localScale.x > 0f)) { localScale.x *= -1f; mFlipTransform.localScale = localScale; }
            }
            else mTransform.localScale = flipX ? new Vector3(-1f, 1f, 1f) : Vector3.one;
        }
        public void ResetPosition() => SetPositionByGrid(mEntity.Grid);
        public void ResetRotation() => mTransform.localRotation = mEntity.DirectionRotation;
        public void ResetScale() => SetScaleByFlipX(mEntity.FlipX);
        public Vector3 GetPosition() => mTransform.localPosition;
        public static bool? GetFlipX(Vector3 fromPos, Vector3 toPos) => toPos.x == fromPos.x ? (bool?)null : toPos.x < fromPos.x;
        public void SetPosition(Vector3 position)
        {
            if (mSetZ)
            {
                float x = position.x - mEntity.RealCenter.x;
                position.z = (position.y - mEntity.RealCenter.y) * 1f + mOffsetZ + mOffsetZPerX * x;
            }
            mTransform.localPosition = position;
        }
        public List<Pos2d> GetClientChooseBodyRange() =>
            mClientOverrideEntityDimensionBounds == null || mClientOverrideEntityDimensionBounds.Count == 0
                ? mEntity.BodyRange : mClientOverrideEntityDimensionBounds;
        public static Vector3 GridToVector3(Pos2d grid) => new Vector3(1f * grid.X, 1f * grid.Y, 0f);
    }
}
