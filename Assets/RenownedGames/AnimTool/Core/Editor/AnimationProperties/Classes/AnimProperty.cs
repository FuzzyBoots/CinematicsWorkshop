/* ================================================================
   ----------------------------------------------------------------
   Project   :   AnimTool
   Publisher :   Renowned Games
   Developer :   Tamerlan Shakirov
   ----------------------------------------------------------------
   Copyright 2022 Renowned Games All rights reserved.
   ================================================================ */

using UnityEngine;

namespace RenownedGames.AnimTool
{
    [System.Serializable]
    public class AnimProperty
    {
        public enum Rotation
        {
            Original,
            BodyOrintation
        }

        public enum PositionY
        {
            Original,
            CenterOfMass,
            Feet
        }

        public enum PositionXZ
        {
            Original,
            CenterOfMass,
        }

        [SerializeField]
        private bool loopTime;

        [SerializeField]
        private bool loopPose;

        [SerializeField]
        private float cycleOffset;

        [SerializeField]
        private bool lockRotation;

        [SerializeField]
        private Rotation rotation;

        [SerializeField]
        private float rotationOffset;

        [SerializeField]
        private bool lockPositionY;

        [SerializeField]
        private PositionY positionY;

        [SerializeField]
        private float positionYOffset;

        [SerializeField]
        private bool lockPositionXZ;

        [SerializeField]
        private PositionXZ positionXZ;

        public AnimProperty()
        {
            this.loopTime = true;
            this.loopPose = false;
            this.cycleOffset = 0.0f;
            this.lockRotation = false;
            this.rotation = Rotation.Original;
            this.rotationOffset = 0.0f;
            this.lockPositionY = false;
            this.positionY = PositionY.Original;
            this.positionYOffset = 0.0f;
            this.lockPositionXZ = false;
            this.positionXZ = PositionXZ.Original;
        }

        public AnimProperty(
            bool loopTime, bool loopPose, float cycleOffset,
            bool lockRotation, Rotation rotation, float rotationOffset,
            bool lockPositionY, PositionY positionY, float positionYOffset,
            bool lockPositionXZ, PositionXZ positionXZ)
        {
            this.loopTime = loopTime;
            this.loopPose = loopPose;
            this.cycleOffset = cycleOffset;
            this.lockRotation = lockRotation;
            this.rotation = rotation;
            this.rotationOffset = rotationOffset;
            this.lockPositionY = lockPositionY;
            this.positionY = positionY;
            this.positionYOffset = positionYOffset;
            this.lockPositionXZ = lockPositionXZ;
            this.positionXZ = positionXZ;
        }

        #region [Getter / Setter]
        public bool LoopTime()
        {
            return loopTime;
        }

        public void LoopTime(bool value)
        {
            loopTime = value;
        }

        public bool LoopPose()
        {
            return loopPose;
        }

        public void LoopPose(bool value)
        {
            loopPose = value;
        }

        public float GetCycleOffset()
        {
            return cycleOffset;
        }

        public void SetCycleOffset(float value)
        {
            cycleOffset = value;
        }

        public bool LockRotation()
        {
            return lockRotation;
        }

        public void LockRotation(bool value)
        {
            lockRotation = value;
        }

        public Rotation GetRotation()
        {
            return rotation;
        }

        public void SetRotation(Rotation value)
        {
            rotation = value;
        }

        public float GetRotationOffset()
        {
            return rotationOffset;
        }

        public void SetRotationOffset(float value)
        {
            rotationOffset = value;
        }

        public bool LockPositionY()
        {
            return lockPositionY;
        }

        public void LockPositionY(bool value)
        {
            lockPositionY = value;
        }

        public PositionY GetPositionY()
        {
            return positionY;
        }

        public void SetPositionY(PositionY value)
        {
            positionY = value;
        }

        public float GetPositionYOffset()
        {
            return positionYOffset;
        }

        public void SetPositionYOffset(float value)
        {
            positionYOffset = value;
        }

        public bool LockPositionXZ()
        {
            return lockPositionXZ;
        }

        public void LockPositionXZ(bool value)
        {
            lockPositionXZ = value;
        }

        public PositionXZ GetPositionXZ()
        {
            return positionXZ;
        }

        public void SetPositionXZ(PositionXZ value)
        {
            positionXZ = value;
        }
        #endregion
    }
}