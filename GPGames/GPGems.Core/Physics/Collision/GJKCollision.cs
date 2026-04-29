using GPGems.Core.Math;
using GPGems.Core.Geometry;

namespace GPGems.Core.Physics.Collision;

/// <summary>
/// GJK (Gilbert-Johnson-Keerthi) 碰撞检测算法
/// 高效检测两个凸多面体之间的碰撞
/// 基于 Game Programming Gems 系列
/// </summary>
public class GJKCollision
{
    public struct Simplex
    {
        private Vector3[] _points;
        private int _count;

        public int Count => _count;

        public Simplex()
        {
            _points = new Vector3[4];
            _count = 0;
        }

        public void Add(Vector3 point)
        {
            _points[3] = _points[2];
            _points[2] = _points[1];
            _points[1] = _points[0];
            _points[0] = point;
            _count = MathUtil.Min(_count + 1, 4);
        }

        public Vector3 this[int index] => _points[index];

        public bool ContainsOrigin(ref Vector3 direction)
        {
            switch (_count)
            {
                case 2: return LineContainsOrigin(ref direction);
                case 3: return TriangleContainsOrigin(ref direction);
                case 4: return TetrahedronContainsOrigin(ref direction);
                default: return false;
            }
        }

        private bool LineContainsOrigin(ref Vector3 direction)
        {
            Vector3 a = _points[0];
            Vector3 b = _points[1];
            Vector3 ab = b - a;
            Vector3 ao = -a;

            if (Vector3.Dot(ab, ao) > 0)
            {
                direction = Vector3.Cross(Vector3.Cross(ab, ao), ab);
            }
            else
            {
                _count = 1;
                direction = ao;
            }

            return direction.LengthSquared() < 1e-6f;
        }

        private bool TriangleContainsOrigin(ref Vector3 direction)
        {
            Vector3 a = _points[0];
            Vector3 b = _points[1];
            Vector3 c = _points[2];
            Vector3 ab = b - a;
            Vector3 ac = c - a;
            Vector3 ao = -a;

            Vector3 abc = Vector3.Cross(ab, ac);

            if (Vector3.Dot(Vector3.Cross(abc, ac), ao) > 0)
            {
                _count = 2;
                _points[0] = a;
                _points[1] = c;
                direction = Vector3.Cross(Vector3.Cross(ac, ao), ac);
            }
            else if (Vector3.Dot(Vector3.Cross(ab, abc), ao) > 0)
            {
                _count = 2;
                _points[0] = a;
                _points[1] = b;
                direction = Vector3.Cross(Vector3.Cross(ab, ao), ab);
            }
            else
            {
                if (Vector3.Dot(abc, ao) > 0)
                {
                    direction = abc;
                }
                else
                {
                    _points[0] = a;
                    _points[1] = c;
                    _points[2] = b;
                    direction = -abc;
                }
            }

            return direction.LengthSquared() < 1e-6f;
        }

        private bool TetrahedronContainsOrigin(ref Vector3 direction)
        {
            Vector3 a = _points[0];
            Vector3 b = _points[1];
            Vector3 c = _points[2];
            Vector3 d = _points[3];
            Vector3 ab = b - a;
            Vector3 ac = c - a;
            Vector3 ad = d - a;
            Vector3 ao = -a;

            Vector3 abc = Vector3.Cross(ab, ac);
            Vector3 acd = Vector3.Cross(ac, ad);
            Vector3 adb = Vector3.Cross(ad, ab);

            if (Vector3.Dot(abc, ao) > 0)
            {
                _count = 3;
                _points[0] = a;
                _points[1] = b;
                _points[2] = c;
                direction = abc;
                return false;
            }

            if (Vector3.Dot(acd, ao) > 0)
            {
                _count = 3;
                _points[0] = a;
                _points[1] = c;
                _points[2] = d;
                direction = acd;
                return false;
            }

            if (Vector3.Dot(adb, ao) > 0)
            {
                _count = 3;
                _points[0] = a;
                _points[1] = d;
                _points[2] = b;
                direction = adb;
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// 检测两个凸形状之间是否碰撞
    /// </summary>
    public static bool DetectCollision(ICollisionShape shapeA, ICollisionShape shapeB)
    {
        var simplex = new Simplex();
        Vector3 direction = shapeB.Center - shapeA.Center;

        if (direction.LengthSquared() < 1e-6f)
            direction = Vector3.UnitX;

        simplex.Add(Support(shapeA, shapeB, direction));
        direction = -direction;

        for (int i = 0; i < 100; i++)
        {
            Vector3 support = Support(shapeA, shapeB, direction);
            if (Vector3.Dot(support, direction) < 0)
                return false;

            simplex.Add(support);

            if (simplex.ContainsOrigin(ref direction))
                return true;
        }

        return false;
    }

    /// <summary>
    /// 碰撞检测并返回最近距离（用于 EPA）
    /// </summary>
    public static bool DetectCollision(ICollisionShape shapeA, ICollisionShape shapeB, out Simplex simplex)
    {
        simplex = new Simplex();
        Vector3 direction = shapeB.Center - shapeA.Center;

        if (direction.LengthSquared() < 1e-6f)
            direction = Vector3.UnitX;

        simplex.Add(Support(shapeA, shapeB, direction));
        direction = -direction;

        for (int i = 0; i < 100; i++)
        {
            Vector3 support = Support(shapeA, shapeB, direction);
            if (Vector3.Dot(support, direction) < 0)
                return false;

            simplex.Add(support);

            if (simplex.ContainsOrigin(ref direction))
                return true;
        }

        return false;
    }

    private static Vector3 Support(ICollisionShape shapeA, ICollisionShape shapeB, Vector3 direction)
    {
        Vector3 supportA = shapeA.Support(direction);
        Vector3 supportB = shapeB.Support(-direction);
        return supportA - supportB;
    }
}

/// <summary>
/// 碰撞形状接口
/// </summary>
public interface ICollisionShape
{
    Vector3 Center { get; }
    Vector3 Support(Vector3 direction);
}

/// <summary>
/// 球体碰撞形状
/// </summary>
public class SphereShape : ICollisionShape
{
    public Vector3 Center { get; set; }
    public float Radius { get; set; }

    public Vector3 Support(Vector3 direction)
    {
        return Center + direction.Normalize() * Radius;
    }
}

/// <summary>
/// 凸多面体碰撞形状
/// </summary>
public class ConvexHullShape : ICollisionShape
{
    public Vector3 Center { get; set; }
    public Vector3[] Vertices { get; set; }

    public ConvexHullShape(Vector3[] vertices)
    {
        Vertices = vertices ?? throw new ArgumentNullException(nameof(vertices));
        Center = ComputeCentroid();
    }

    private Vector3 ComputeCentroid()
    {
        Vector3 centroid = Vector3.Zero;
        foreach (var v in Vertices)
            centroid += v;
        return centroid / Vertices.Length;
    }

    public Vector3 Support(Vector3 direction)
    {
        float maxDot = float.MinValue;
        Vector3 maxVertex = Center;

        foreach (var v in Vertices)
        {
            float dot = Vector3.Dot(v, direction);
            if (dot > maxDot)
            {
                maxDot = dot;
                maxVertex = v;
            }
        }

        return maxVertex;
    }
}

/// <summary>
/// Box 碰撞形状
/// </summary>
public class BoxShape : ICollisionShape
{
    public Vector3 Center { get; set; }
    public Vector3 HalfExtents { get; set; }
    public Quaternion Orientation { get; set; }

    public BoxShape(Vector3 center, Vector3 halfExtents)
    {
        Center = center;
        HalfExtents = halfExtents;
        Orientation = Quaternion.Identity;
    }

    public Vector3 Support(Vector3 direction)
    {
        var localDir = Orientation.Conjugate().Rotate(direction);
        var localSupport = new Vector3(
            MathF.Sign(localDir.X) * HalfExtents.X,
            MathF.Sign(localDir.Y) * HalfExtents.Y,
            MathF.Sign(localDir.Z) * HalfExtents.Z
        );
        return Center + Orientation.Rotate(localSupport);
    }
}
