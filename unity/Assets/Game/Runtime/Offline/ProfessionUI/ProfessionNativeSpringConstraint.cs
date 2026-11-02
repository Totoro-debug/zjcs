using System;

namespace Zjcs.Offline.ProfessionUI
{
    // Independent planar spring dynamics for the translation and rotation
    // channels present in the selected player assets. The host owns constraint
    // ordering, animation sampling, skin activation and the skeleton clock.
    public sealed class ProfessionNativeSpringConstraint
    {
        public struct Parameters
        {
            public float Step, X, Y, Turn, Limit, Inertia, Strength, Damping;
            public float InverseMass, Wind, Gravity, Mix, Reference;
        }

        public struct Affine
        {
            public float A, B, C, D, X, Y, Length;
        }

        private bool initialized;
        private float lastTime, residual, previousX, previousY, physicalX, physicalY, tipX, tipY;
        private float offsetX, offsetY, angle, velocityX, velocityY, angularVelocity;

        public void Reset(Affine pose, float time)
        {
            Validate(pose); ValidateTime(time);
            lastTime = time;
            residual = offsetX = offsetY = angle = velocityX = velocityY = angularVelocity = 0;
            physicalX = previousX = pose.X; physicalY = previousY = pose.Y;
            tipX = pose.A * pose.Length; tipY = pose.C * pose.Length;
            initialized = true;
        }

        public Affine Evaluate(Affine pose, Parameters parameters, float time)
        {
            if (!initialized) throw new InvalidOperationException("Bind the initial original world pose before advancing physics.");
            Validate(pose); Validate(parameters); ValidateTime(time);
            var p = parameters;
            if (p.Mix == 0) return pose;
            float dt = Math.Max(0, time - lastTime);
            lastTime = time;
            residual += dt;

            // Translation inertia is limited after weighting. Angular motion
            // below limits the movement of the translated pivot separately.
            float dx = Clamp((previousX - pose.X) * p.Inertia, p.Limit * dt);
            float dy = Clamp((previousY - pose.Y) * p.Inertia, p.Limit * dt);
            previousX = pose.X; previousY = pose.Y;
            offsetX += dx; offsetY += dy;

            float baseAngle = (float)Math.Atan2(pose.C, pose.A);
            float damping = (float)Math.Pow(p.Damping, p.Step * 60);
            int steps = 0;
            while (residual >= p.Step)
            {
                float remaining = residual - p.Step;
                if (remaining == residual) throw new ArgumentOutOfRangeException(nameof(time), "Physics time cannot be resolved at the original step size.");
                residual = remaining;
                steps = checked(steps + 1);
                velocityX = velocityX * damping + (p.Wind * p.Reference - offsetX * p.Strength) * p.InverseMass * p.Step;
                velocityY = velocityY * damping + (-p.Gravity * p.Reference - offsetY * p.Strength) * p.InverseMass * p.Step;
                offsetX += velocityX * p.Step; offsetY += velocityY * p.Step;
            }

            pose.X += offsetX * p.Mix * p.X; pose.Y += offsetY * p.Mix * p.Y;
            dx = Clamp(physicalX - pose.X, p.Limit * dt); dy = Clamp(physicalY - pose.Y, p.Limit * dt);
            physicalX = pose.X; physicalY = pose.Y;
            float direction = (float)Math.Atan2(tipY + dy, tipX + dx);
            angle += Wrap(direction - baseAngle - angle * p.Mix * p.Turn) * p.Inertia;

            for (int index = 0; index < steps; index++)
            {
                float applied = baseAngle + angle * p.Mix * p.Turn;
                float force = (-p.Wind * (float)Math.Sin(applied) - p.Gravity * (float)Math.Cos(applied)) * pose.Length / p.Reference;
                angularVelocity = angularVelocity * damping + (force - angle * p.Strength) * p.InverseMass * p.Step;
                angle += angularVelocity * p.Step;
            }

            float rotation = angle * p.Mix * p.Turn;
            float cosine = (float)Math.Cos(rotation), sine = (float)Math.Sin(rotation);
            float a = pose.A * cosine - pose.C * sine, b = pose.B * cosine - pose.D * sine;
            pose.C = pose.A * sine + pose.C * cosine; pose.D = pose.B * sine + pose.D * cosine;
            pose.A = a; pose.B = b;
            tipX = pose.A * pose.Length; tipY = pose.C * pose.Length;
            Validate(pose);
            return pose;
        }

        private static float Clamp(float value, float limit) => Math.Max(-limit, Math.Min(limit, value));
        private static float Wrap(float value)
        {
            const float pi = (float)Math.PI;
            while (value > pi) value -= 2 * pi;
            while (value < -pi) value += 2 * pi;
            return value;
        }
        private static void ValidateTime(float time)
        {
            Finite(time);
            if (time < 0) throw new ArgumentOutOfRangeException(nameof(time));
        }
        private static void Validate(Affine pose)
        {
            Finite(pose.A); Finite(pose.B); Finite(pose.C); Finite(pose.D);
            Finite(pose.X); Finite(pose.Y); Finite(pose.Length);
            if (pose.Length < 0) throw new ArgumentOutOfRangeException(nameof(pose));
        }
        private static void Validate(Parameters p)
        {
            Finite(p.Step); Finite(p.X); Finite(p.Y); Finite(p.Turn); Finite(p.Limit);
            Finite(p.Inertia); Finite(p.Strength); Finite(p.Damping); Finite(p.InverseMass);
            Finite(p.Wind); Finite(p.Gravity); Finite(p.Mix); Finite(p.Reference);
            if (p.Step <= 0 || p.Reference <= 0 || p.Limit < 0 || p.Damping < 0)
                throw new ArgumentOutOfRangeException(nameof(p));
        }
        private static void Finite(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentException("Original physics inputs and outputs must be finite.");
        }
    }
}
