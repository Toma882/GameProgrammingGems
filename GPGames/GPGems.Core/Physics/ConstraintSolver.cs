using GPGems.Core.Math;
using GPGems.Core.Physics.Collision;

namespace GPGems.Core.Physics;

/// <summary>
/// 约束求解器
/// 基于脉冲的约束求解，用于关节和碰撞响应
/// 基于 Game Programming Gems 系列
/// </summary>
public class ConstraintSolver
{
    private const int DefaultIterations = 10;

    public int Iterations { get; set; } = DefaultIterations;

    private readonly List<ContactConstraint> _contacts = new();
    private readonly List<IJoint> _joints = new();

    public void AddContact(ContactPoint contact, RigidBody bodyA, RigidBody bodyB)
    {
        _contacts.Add(new ContactConstraint(contact, bodyA, bodyB));
    }

    public void AddJoint(IJoint joint)
    {
        _joints.Add(joint);
    }

    public void Solve(float deltaTime)
    {
        foreach (var contact in _contacts)
            contact.Prepare(deltaTime);

        foreach (var joint in _joints)
            joint.Prepare(deltaTime);

        for (int i = 0; i < Iterations; i++)
        {
            foreach (var contact in _contacts)
                contact.Solve();

            foreach (var joint in _joints)
                joint.Solve();
        }
    }

    public void Clear()
    {
        _contacts.Clear();
        _joints.Clear();
    }
}

/// <summary>
/// 接触约束
/// </summary>
public class ContactConstraint
{
    private ContactPoint _contact;
    private readonly RigidBody _bodyA;
    private readonly RigidBody _bodyB;
    private float _effectiveMass;
    private Vector3 _ra;
    private Vector3 _rb;
    private float _bias;

    public ContactConstraint(ContactPoint contact, RigidBody bodyA, RigidBody bodyB)
    {
        _contact = contact;
        _bodyA = bodyA;
        _bodyB = bodyB;
    }

    public void Prepare(float deltaTime)
    {
        _ra = _contact.PositionA - _bodyA.Position;
        _rb = _contact.PositionB - _bodyB.Position;

        var normal = _contact.Normal;

        float invMassA = _bodyA.IsStatic ? 0 : _bodyA.State.InverseMass;
        float invMassB = _bodyB.IsStatic ? 0 : _bodyB.State.InverseMass;

        float k = invMassA + invMassB;

        if (!_bodyA.IsStatic)
        {
            var raCrossN = Vector3.Cross(_ra, normal);
            var rotA = GetWorldInverseInertia(_bodyA);
            k += Vector3.Dot(raCrossN, rotA * raCrossN);
        }

        if (!_bodyB.IsStatic)
        {
            var rbCrossN = Vector3.Cross(_rb, normal);
            var rotB = GetWorldInverseInertia(_bodyB);
            k += Vector3.Dot(rbCrossN, rotB * rbCrossN);
        }

        _effectiveMass = k > 1e-6f ? 1 / k : 0;

        Vector3 relVel = CalculateRelativeVelocity();
        float velAlongNormal = Vector3.Dot(relVel, normal);

        float restitution = MathF.Max(_bodyA.State.Restitution, _bodyB.State.Restitution);
        float biasFactor = 0.2f;
        float slop = 0.01f;
        float penetration = MathF.Max(_contact.Penetration - slop, 0);

        _bias = -biasFactor / deltaTime * penetration + restitution * MathF.Max(-velAlongNormal, 0);
    }

    public void Solve()
    {
        var normal = _contact.Normal;
        float relVel = Vector3.Dot(CalculateRelativeVelocity(), normal);

        float lambda = (-relVel + _bias) * _effectiveMass;

        float oldImpulse = _contact.Impulse;
        _contact.Impulse = MathF.Max(oldImpulse + lambda, 0);
        float deltaImpulse = _contact.Impulse - oldImpulse;

        var impulse = normal * deltaImpulse;

        if (!_bodyA.IsStatic)
            _bodyA.ApplyImpulseAtWorldPosition(-impulse, _contact.PositionA);

        if (!_bodyB.IsStatic)
            _bodyB.ApplyImpulseAtWorldPosition(impulse, _contact.PositionB);

        SolveFriction();
    }

    private void SolveFriction()
    {
        var normal = _contact.Normal;
        var relVel = CalculateRelativeVelocity();
        var tangent = (relVel - normal * Vector3.Dot(relVel, normal)).Normalize();

        if (tangent.LengthSquared() < 1e-6f)
            return;

        float invMassA = _bodyA.IsStatic ? 0 : _bodyA.State.InverseMass;
        float invMassB = _bodyB.IsStatic ? 0 : _bodyB.State.InverseMass;

        float k = invMassA + invMassB;

        if (!_bodyA.IsStatic)
        {
            var raCrossT = Vector3.Cross(_ra, tangent);
            k += Vector3.Dot(raCrossT, GetWorldInverseInertia(_bodyA) * raCrossT);
        }

        if (!_bodyB.IsStatic)
        {
            var rbCrossT = Vector3.Cross(_rb, tangent);
            k += Vector3.Dot(rbCrossT, GetWorldInverseInertia(_bodyB) * rbCrossT);
        }

        float frictionMass = k > 1e-6f ? 1 / k : 0;
        float friction = MathF.Sqrt(_bodyA.State.Friction * _bodyB.State.Friction);
        float maxFriction = friction * _contact.Impulse;

        float lambda = -Vector3.Dot(relVel, tangent) * frictionMass;

        float oldFrictionImpulse = _contact.FrictionImpulse;
        _contact.FrictionImpulse = MathUtil.Clamp(oldFrictionImpulse + lambda, -maxFriction, maxFriction);
        float deltaImpulse = _contact.FrictionImpulse - oldFrictionImpulse;

        var frictionImpulse = tangent * deltaImpulse;

        if (!_bodyA.IsStatic)
            _bodyA.ApplyImpulseAtWorldPosition(-frictionImpulse, _contact.PositionA);

        if (!_bodyB.IsStatic)
            _bodyB.ApplyImpulseAtWorldPosition(frictionImpulse, _contact.PositionB);
    }

    private Vector3 CalculateRelativeVelocity()
    {
        var velA = _bodyA.IsStatic ? Vector3.Zero : _bodyA.LinearVelocity + Vector3.Cross(_bodyA.AngularVelocity, _ra);
        var velB = _bodyB.IsStatic ? Vector3.Zero : _bodyB.LinearVelocity + Vector3.Cross(_bodyB.AngularVelocity, _rb);
        return velB - velA;
    }

    private static Matrix3x3 GetWorldInverseInertia(RigidBody body)
    {
        float x = body.Orientation.X;
        float y = body.Orientation.Y;
        float z = body.Orientation.Z;
        float w = body.Orientation.W;

        float xx = x * x, yy = y * y, zz = z * z;
        float xy = x * y, xz = x * z, yz = y * z;
        float wx = w * x, wy = w * y, wz = w * z;

        var rot = new Matrix3x3(
            1 - 2 * (yy + zz), 2 * (xy - wz), 2 * (xz + wy),
            2 * (xy + wz), 1 - 2 * (xx + zz), 2 * (yz - wx),
            2 * (xz - wy), 2 * (yz + wx), 1 - 2 * (xx + yy)
        );

        return rot * body.State.InverseInertiaTensor * rot.Transpose();
    }
}

/// <summary>
/// 关节接口
/// </summary>
public interface IJoint
{
    void Prepare(float deltaTime);
    void Solve();
}

/// <summary>
/// 球铰关节
/// </summary>
public class BallJoint : IJoint
{
    private readonly RigidBody _bodyA;
    private readonly RigidBody _bodyB;
    private readonly Vector3 _localAnchorA;
    private readonly Vector3 _localAnchorB;
    private Vector3 _worldAnchorA;
    private Vector3 _worldAnchorB;
    private Matrix3x3 _effectiveMass;

    public BallJoint(RigidBody bodyA, RigidBody bodyB, Vector3 worldAnchor)
    {
        _bodyA = bodyA;
        _bodyB = bodyB;
        _worldAnchorA = worldAnchor;
        _worldAnchorB = worldAnchor;
        _localAnchorA = bodyA.Orientation.Conjugate().Rotate(worldAnchor - bodyA.Position);
        _localAnchorB = bodyB.Orientation.Conjugate().Rotate(worldAnchor - bodyB.Position);
    }

    public void Prepare(float deltaTime)
    {
        _worldAnchorA = _bodyA.Position + _bodyA.Orientation.Rotate(_localAnchorA);
        _worldAnchorB = _bodyB.Position + _bodyB.Orientation.Rotate(_localAnchorB);

        var ra = _worldAnchorA - _bodyA.Position;
        var rb = _worldAnchorB - _bodyB.Position;

        Matrix3x3 k = Matrix3x3.CreateScale(
            _bodyA.State.InverseMass + _bodyB.State.InverseMass,
            _bodyA.State.InverseMass + _bodyB.State.InverseMass,
            _bodyA.State.InverseMass + _bodyB.State.InverseMass
        );

        // 添加转动惯量贡献（简化实现）
        _effectiveMass = k.Inverse();
    }

    public void Solve()
    {
        var error = _worldAnchorB - _worldAnchorA;
        var impulse = _effectiveMass * error * 0.1f;

        if (!_bodyA.IsStatic)
            _bodyA.ApplyImpulseAtWorldPosition(-impulse, _worldAnchorA);

        if (!_bodyB.IsStatic)
            _bodyB.ApplyImpulseAtWorldPosition(impulse, _worldAnchorB);
    }
}
