using System.Numerics;
using GPGems.Core.Math;
using GPGems.Core.Geometry;
using GPGems.Core.Physics.Collision;

namespace GPGems.Core.Physics;

/// <summary>
/// 刚体物理属性
/// </summary>
public struct RigidBodyState
{
    public Vector3 Position;
    public Quaternion Orientation;
    public Vector3 LinearVelocity;
    public Vector3 AngularVelocity;
    public Vector3 ForceAccumulator;
    public Vector3 TorqueAccumulator;
    public float Mass;
    public float InverseMass;
    public Matrix3x3 InertiaTensor;
    public Matrix3x3 InverseInertiaTensor;
    public float LinearDamping;
    public float AngularDamping;
    public float Restitution;
    public float Friction;
}

/// <summary>
/// 3x3 矩阵
/// </summary>
public readonly struct Matrix3x3
{
    public readonly float M11, M12, M13;
    public readonly float M21, M22, M23;
    public readonly float M31, M32, M33;

    public Matrix3x3(float m11, float m12, float m13,
                     float m21, float m22, float m23,
                     float m31, float m32, float m33)
    {
        M11 = m11; M12 = m12; M13 = m13;
        M21 = m21; M22 = m22; M23 = m23;
        M31 = m31; M32 = m32; M33 = m33;
    }

    public static Matrix3x3 Identity => new(1, 0, 0, 0, 1, 0, 0, 0, 1);

    public static Matrix3x3 CreateScale(float sx, float sy, float sz)
    {
        return new Matrix3x3(sx, 0, 0, 0, sy, 0, 0, 0, sz);
    }

    public static Vector3 operator *(Matrix3x3 m, Vector3 v)
    {
        return new Vector3(
            m.M11 * v.X + m.M12 * v.Y + m.M13 * v.Z,
            m.M21 * v.X + m.M22 * v.Y + m.M23 * v.Z,
            m.M31 * v.X + m.M32 * v.Y + m.M33 * v.Z
        );
    }

    public static Matrix3x3 operator *(Matrix3x3 a, Matrix3x3 b)
    {
        return new Matrix3x3(
            a.M11 * b.M11 + a.M12 * b.M21 + a.M13 * b.M31,
            a.M11 * b.M12 + a.M12 * b.M22 + a.M13 * b.M32,
            a.M11 * b.M13 + a.M12 * b.M23 + a.M13 * b.M33,
            a.M21 * b.M11 + a.M22 * b.M21 + a.M23 * b.M31,
            a.M21 * b.M12 + a.M22 * b.M22 + a.M23 * b.M32,
            a.M21 * b.M13 + a.M22 * b.M23 + a.M23 * b.M33,
            a.M31 * b.M11 + a.M32 * b.M21 + a.M33 * b.M31,
            a.M31 * b.M12 + a.M32 * b.M22 + a.M33 * b.M32,
            a.M31 * b.M13 + a.M32 * b.M23 + a.M33 * b.M33
        );
    }

    public Matrix3x3 Transpose()
    {
        return new Matrix3x3(
            M11, M21, M31,
            M12, M22, M32,
            M13, M23, M33
        );
    }

    public Matrix3x3 Inverse()
    {
        float det = M11 * (M22 * M33 - M23 * M32) -
                    M12 * (M21 * M33 - M23 * M31) +
                    M13 * (M21 * M32 - M22 * M31);

        if (MathF.Abs(det) < 1e-6f)
            return Identity;

        float invDet = 1 / det;

        return new Matrix3x3(
            invDet * (M22 * M33 - M23 * M32),
            invDet * (M13 * M32 - M12 * M33),
            invDet * (M12 * M23 - M13 * M22),
            invDet * (M23 * M31 - M21 * M33),
            invDet * (M11 * M33 - M13 * M31),
            invDet * (M21 * M13 - M11 * M23),
            invDet * (M21 * M32 - M22 * M31),
            invDet * (M12 * M31 - M11 * M32),
            invDet * (M11 * M22 - M12 * M21)
        );
    }
}

/// <summary>
/// 刚体
/// 基于 Game Programming Gems 的物理实现
/// </summary>
public class RigidBody
{
    private RigidBodyState _state;

    public ref RigidBodyState State => ref _state;

    public Vector3 Position
    {
        get => _state.Position;
        set => _state.Position = value;
    }

    public Quaternion Orientation
    {
        get => _state.Orientation;
        set => _state.Orientation = value;
    }

    public Vector3 LinearVelocity
    {
        get => _state.LinearVelocity;
        set => _state.LinearVelocity = value;
    }

    public Vector3 AngularVelocity
    {
        get => _state.AngularVelocity;
        set => _state.AngularVelocity = value;
    }

    public float Mass
    {
        get => _state.Mass;
        set
        {
            _state.Mass = value;
            _state.InverseMass = value > 0 ? 1 / value : 0;
        }
    }

    public bool IsStatic => _state.InverseMass == 0;

    public RigidBody(float mass = 1f)
    {
        _state = new RigidBodyState
        {
            Mass = mass,
            InverseMass = mass > 0 ? 1 / mass : 0,
            Orientation = Quaternion.Identity,
            InertiaTensor = Matrix3x3.Identity,
            InverseInertiaTensor = Matrix3x3.Identity,
            LinearDamping = 0.99f,
            AngularDamping = 0.95f,
            Restitution = 0.6f,
            Friction = 0.3f
        };
    }

    /// <summary>设置盒形惯性张量</summary>
    public void SetBoxInertia(float halfX, float halfY, float halfZ)
    {
        float factor = _state.Mass / 12f;
        _state.InertiaTensor = Matrix3x3.CreateScale(
            factor * (4 * halfY * halfY + 4 * halfZ * halfZ),
            factor * (4 * halfX * halfX + 4 * halfZ * halfZ),
            factor * (4 * halfX * halfX + 4 * halfY * halfY)
        );
        _state.InverseInertiaTensor = _state.InertiaTensor.Inverse();
    }

    /// <summary>设置球形惯性张量</summary>
    public void SetSphereInertia(float radius)
    {
        float i = 0.4f * _state.Mass * radius * radius;
        _state.InertiaTensor = Matrix3x3.CreateScale(i, i, i);
        _state.InverseInertiaTensor = _state.InertiaTensor.Inverse();
    }

    /// <summary>施加力</summary>
    public void ApplyForce(Vector3 force)
    {
        _state.ForceAccumulator += force;
    }

    /// <summary>在世界空间位置施加力</summary>
    public void ApplyForceAtWorldPosition(Vector3 force, Vector3 worldPos)
    {
        _state.ForceAccumulator += force;
        Vector3 localPos = worldPos - _state.Position;
        _state.TorqueAccumulator += Vector3.Cross(localPos, force);
    }

    /// <summary>施加冲量</summary>
    public void ApplyImpulse(Vector3 impulse)
    {
        _state.LinearVelocity += impulse * _state.InverseMass;
    }

    /// <summary>在世界空间位置施加冲量</summary>
    public void ApplyImpulseAtWorldPosition(Vector3 impulse, Vector3 worldPos)
    {
        _state.LinearVelocity += impulse * _state.InverseMass;
        Vector3 localPos = worldPos - _state.Position;

        var rotMatrix = GetRotationMatrix();
        var worldInvInertia = rotMatrix * _state.InverseInertiaTensor * rotMatrix.Transpose();

        var torqueImpulse = Vector3.Cross(localPos, impulse);
        _state.AngularVelocity += worldInvInertia * torqueImpulse;
    }

    /// <summary>积分物理状态</summary>
    public void Integrate(float deltaTime)
    {
        if (IsStatic)
            return;

        _state.LinearVelocity += _state.ForceAccumulator * _state.InverseMass * deltaTime;
        _state.LinearVelocity *= MathF.Pow(_state.LinearDamping, deltaTime);
        _state.Position += _state.LinearVelocity * deltaTime;

        var rotMatrix = GetRotationMatrix();
        var worldInvInertia = rotMatrix * _state.InverseInertiaTensor * rotMatrix.Transpose();
        _state.AngularVelocity += worldInvInertia * _state.TorqueAccumulator * deltaTime;
        _state.AngularVelocity *= MathF.Pow(_state.AngularDamping, deltaTime);

        var deltaQ = new Quaternion(_state.AngularVelocity.X, _state.AngularVelocity.Y, _state.AngularVelocity.Z, 0) * _state.Orientation;
        _state.Orientation = new Quaternion(
            _state.Orientation.X + deltaQ.X * 0.5f * deltaTime,
            _state.Orientation.Y + deltaQ.Y * 0.5f * deltaTime,
            _state.Orientation.Z + deltaQ.Z * 0.5f * deltaTime,
            _state.Orientation.W + deltaQ.W * 0.5f * deltaTime
        ).Normalize();

        ClearAccumulators();
    }

    private Matrix3x3 GetRotationMatrix()
    {
        var q = _state.Orientation;
        float xx = q.X * q.X;
        float yy = q.Y * q.Y;
        float zz = q.Z * q.Z;
        float xy = q.X * q.Y;
        float xz = q.X * q.Z;
        float yz = q.Y * q.Z;
        float wx = q.W * q.X;
        float wy = q.W * q.Y;
        float wz = q.W * q.Z;

        return new Matrix3x3(
            1 - 2 * (yy + zz), 2 * (xy - wz), 2 * (xz + wy),
            2 * (xy + wz), 1 - 2 * (xx + zz), 2 * (yz - wx),
            2 * (xz - wy), 2 * (yz + wx), 1 - 2 * (xx + yy)
        );
    }

    public void ClearAccumulators()
    {
        _state.ForceAccumulator = Vector3.Zero;
        _state.TorqueAccumulator = Vector3.Zero;
    }
}
