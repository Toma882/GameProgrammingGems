using System.Numerics;
using System.Numerics;
using GPGems.Core.Math;
using GPGems.Core.Geometry;

namespace GPGems.Core.Physics.Collision;

/// <summary>
/// 接触点
/// </summary>
public struct ContactPoint
{
    public Vector3 PositionA;
    public Vector3 PositionB;
    public Vector3 Normal;
    public float Penetration;
    public float Impulse;
    public float FrictionImpulse;
}

/// <summary>
/// 接触流形
/// 存储两个碰撞物体之间的所有接触点
/// 用于稳定的物理响应
/// </summary>
public class ContactManifold
{
    private const int MaxContacts = 4;
    private readonly List<ContactPoint> _contacts = new();

    public IReadOnlyList<ContactPoint> Contacts => _contacts;
    public int ContactCount => _contacts.Count;

    /// <summary>添加接触点</summary>
    public void AddContact(ContactPoint contact)
    {
        for (int i = 0; i < _contacts.Count; i++)
        {
            float dist = Vector3.Distance(contact.PositionA, _contacts[i].PositionA);
            if (dist < 0.01f)
            {
                _contacts[i] = MergeContacts(_contacts[i], contact);
                return;
            }
        }

        if (_contacts.Count < MaxContacts)
        {
            _contacts.Add(contact);
        }
        else
        {
            PruneContacts(contact);
        }
    }

    private ContactPoint MergeContacts(ContactPoint a, ContactPoint b)
    {
        return new ContactPoint
        {
            PositionA = (a.PositionA + b.PositionA) * 0.5f,
            PositionB = (a.PositionB + b.PositionB) * 0.5f,
            Normal = (a.Normal + b.Normal).Normalize(),
            Penetration = MathF.Max(a.Penetration, b.Penetration),
            Impulse = 0,
            FrictionImpulse = 0
        };
    }

    private void PruneContacts(ContactPoint newContact)
    {
        float maxArea = 0;
        int removeIndex = 0;

        for (int i = 0; i < _contacts.Count; i++)
        {
            float area = ComputeAreaWithout(i, newContact);
            if (area > maxArea)
            {
                maxArea = area;
                removeIndex = i;
            }
        }

        _contacts[removeIndex] = newContact;
    }

    private float ComputeAreaWithout(int skipIndex, ContactPoint newPoint)
    {
        var points = new List<Vector3>();

        for (int i = 0; i < _contacts.Count; i++)
        {
            if (i != skipIndex)
                points.Add(_contacts[i].PositionA);
        }

        points.Add(newPoint.PositionA);

        if (points.Count < 3)
            return 0;

        float area = 0;
        for (int i = 2; i < points.Count; i++)
        {
            var e1 = points[i - 1] - points[0];
            var e2 = points[i] - points[0];
            area += Vector3.Cross(e1, e2).Length();
        }

        return area;
    }

    /// <summary>清除所有接触点</summary>
    public void Clear()
    {
        _contacts.Clear();
    }

    /// <summary>根据 EPA 结果生成接触流形</summary>
    public static ContactManifold CreateFromPenetration(
        ICollisionShape shapeA,
        ICollisionShape shapeB,
        PenetrationInfo penetration)
    {
        var manifold = new ContactManifold();

        if (!penetration.HasCollision)
            return manifold;

        Vector3 pointOnA = shapeA.Support(-penetration.Normal);
        Vector3 pointOnB = shapeB.Support(penetration.Normal);

        manifold.AddContact(new ContactPoint
        {
            PositionA = pointOnA,
            PositionB = pointOnB,
            Normal = penetration.Normal,
            Penetration = penetration.Depth,
            Impulse = 0,
            FrictionImpulse = 0
        });

        return manifold;
    }
}
