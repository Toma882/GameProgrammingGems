using GPGems.Core.Math;

namespace GPGems.Core.Geometry;

/// <summary>
/// 多边形顶点
/// 包含位置、法向量、UV等信息
/// </summary>
public readonly struct Vertex
{
    public Vector3 Position { get; }
    public Vector3 Normal { get; }
    public Vector2 UV { get; }

    public Vertex(Vector3 position)
    {
        Position = position;
        Normal = Vector3.Zero;
        UV = Vector2.Zero;
    }

    public Vertex(Vector3 position, Vector3 normal)
    {
        Position = position;
        Normal = normal;
        UV = Vector2.Zero;
    }

    public Vertex(Vector3 position, Vector3 normal, Vector2 uv)
    {
        Position = position;
        Normal = normal;
        UV = uv;
    }

    public static Vertex Lerp(Vertex a, Vertex b, float t)
    {
        return new Vertex(
            a.Position + (b.Position - a.Position) * t,
            a.Normal + (b.Normal - a.Normal) * t,
            a.UV + (b.UV - a.UV) * t
        );
    }
}

/// <summary>
/// 凸多边形
/// 用于 BSP 树和 CSG 运算
/// 基于 Game Programming Gems 1 Chapter 1.5
/// </summary>
public class Polygon
{
    private readonly List<Vertex> _vertices;

    public IReadOnlyList<Vertex> Vertices => _vertices;
    public Plane Plane { get; private set; }
    public object? UserData { get; set; }
    public int VertexCount => _vertices.Count;

    public Polygon(List<Vertex> vertices)
    {
        _vertices = vertices ?? throw new ArgumentNullException(nameof(vertices));
        if (vertices.Count < 3) throw new ArgumentException("Polygon requires at least 3 vertices");
        UpdatePlane();
    }

    public Polygon(params Vertex[] vertices) : this(new List<Vertex>(vertices))
    {
    }

    public void UpdatePlane()
    {
        if (_vertices.Count >= 3)
        {
            Plane = Plane.FromPoints(
                _vertices[0].Position,
                _vertices[1].Position,
                _vertices[2].Position
            );
        }
    }

    public void Flip()
    {
        _vertices.Reverse();
        UpdatePlane();
    }

    public Polygon Flipped()
    {
        var flipped = new Polygon(new List<Vertex>(_vertices));
        flipped.Flip();
        flipped.UserData = UserData;
        return flipped;
    }

    public PlaneSide SplitByPlane(Plane plane, out Polygon? front, out Polygon? back, float epsilon = 1e-6f)
    {
        front = null;
        back = null;

        var frontVerts = new List<Vertex>();
        var backVerts = new List<Vertex>();

        int count = _vertices.Count;
        for (int i = 0; i < count; i++)
        {
            int j = (i + 1) % count;
            var vi = _vertices[i];
            var vj = _vertices[j];

            float di = plane.DistanceToPoint(vi.Position);
            float dj = plane.DistanceToPoint(vj.Position);

            if (di > epsilon) frontVerts.Add(vi);
            else if (di < -epsilon) backVerts.Add(vi);
            else { frontVerts.Add(vi); backVerts.Add(vi); }

            if ((di > epsilon && dj < -epsilon) || (di < -epsilon && dj > epsilon))
            {
                float t = di / (di - dj);
                var intersection = Vertex.Lerp(vi, vj, t);
                frontVerts.Add(intersection);
                backVerts.Add(intersection);
            }
        }

        if (frontVerts.Count >= 3) front = new Polygon(frontVerts) { UserData = UserData };
        if (backVerts.Count >= 3) back = new Polygon(backVerts) { UserData = UserData };

        if (frontVerts.Count >= 3 && backVerts.Count >= 3) return PlaneSide.Spanning;
        if (frontVerts.Count >= 3) return PlaneSide.Front;
        if (backVerts.Count >= 3) return PlaneSide.Back;
        return PlaneSide.OnPlane;
    }

    public Bounds ComputeBounds()
    {
        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

        foreach (var v in _vertices)
        {
            min = new Vector3(
                MathF.Min(min.X, v.Position.X),
                MathF.Min(min.Y, v.Position.Y),
                MathF.Min(min.Z, v.Position.Z)
            );
            max = new Vector3(
                MathF.Max(max.X, v.Position.X),
                MathF.Max(max.Y, v.Position.Y),
                MathF.Max(max.Z, v.Position.Z)
            );
        }

        return Bounds.FromMinMax(min, max);
    }

    public float ComputeArea()
    {
        float area = 0;
        for (int i = 2; i < _vertices.Count; i++)
        {
            var e1 = _vertices[i - 1].Position - _vertices[0].Position;
            var e2 = _vertices[i].Position - _vertices[0].Position;
            area += Vector3.Cross(e1, e2).Length() * 0.5f;
        }
        return area;
    }
}
