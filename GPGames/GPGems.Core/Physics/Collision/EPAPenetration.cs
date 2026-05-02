using System.Numerics;
using System.Numerics;
using GPGems.Core.Math;
using GPGems.Core.Geometry;

namespace GPGems.Core.Physics.Collision;

/// <summary>
/// EPA (Expanding Polytope Algorithm)
/// 计算两个碰撞凸多面体之间的穿透深度和法向量
/// 配合 GJK 使用，用于碰撞响应
/// </summary>
public class EPAPenetration
{
    private struct Face
    {
        public int A, B, C;
        public Vector3 Normal;
        public float Distance;

        public Face(int a, int b, int c, Vector3[] vertices)
        {
            A = a;
            B = b;
            C = c;

            Vector3 ab = vertices[b] - vertices[a];
            Vector3 ac = vertices[c] - vertices[a];
            Normal = Vector3.Cross(ab, ac).Normalize();
            Distance = Vector3.Dot(Normal, vertices[a]);
        }
    }

    /// <summary>
    /// 计算穿透信息
    /// </summary>
    public static PenetrationInfo Compute(ICollisionShape shapeA, ICollisionShape shapeB, int maxIterations = 50)
    {
        if (!GJKCollision.DetectCollision(shapeA, shapeB, out var simplex))
            return PenetrationInfo.NoCollision;

        var vertices = new List<Vector3>(4);
        for (int i = 0; i < simplex.Count; i++)
            vertices.Add(simplex[i]);

        var faces = new List<Face>
        {
            new Face(0, 1, 2, vertices.ToArray()),
            new Face(0, 2, 3, vertices.ToArray()),
            new Face(0, 3, 1, vertices.ToArray()),
            new Face(1, 3, 2, vertices.ToArray())
        };

        for (int i = 0; i < maxIterations; i++)
        {
            int closest = FindClosestFace(faces);
            var closestFace = faces[closest];

            Vector3 support = Support(shapeA, shapeB, closestFace.Normal);
            float supportDist = Vector3.Dot(support, closestFace.Normal);

            if (supportDist - closestFace.Distance < 1e-3f)
            {
                return new PenetrationInfo
                {
                    Normal = closestFace.Normal,
                    Depth = supportDist,
                    HasCollision = true
                };
            }

            ExpandPolytope(vertices, faces, support);
        }

        var finalClosest = FindClosestFace(faces);
        var finalFace = faces[finalClosest];

        return new PenetrationInfo
        {
            Normal = finalFace.Normal,
            Depth = Vector3.Dot(Support(shapeA, shapeB, finalFace.Normal), finalFace.Normal),
            HasCollision = true
        };
    }

    private static int FindClosestFace(List<Face> faces)
    {
        int closest = 0;
        float minDist = float.MaxValue;

        for (int i = 0; i < faces.Count; i++)
        {
            if (faces[i].Distance < minDist)
            {
                minDist = faces[i].Distance;
                closest = i;
            }
        }

        return closest;
    }

    private static void ExpandPolytope(List<Vector3> vertices, List<Face> faces, Vector3 support)
    {
        int newIndex = vertices.Count;
        vertices.Add(support);

        var edges = new List<(int, int)>();

        for (int i = faces.Count - 1; i >= 0; i--)
        {
            var face = faces[i];
            Vector3 faceCenter = (vertices[face.A] + vertices[face.B] + vertices[face.C]) / 3;
            Vector3 toSupport = support - faceCenter;

            if (Vector3.Dot(toSupport, face.Normal) > 0)
            {
                AddEdge(edges, face.A, face.B);
                AddEdge(edges, face.B, face.C);
                AddEdge(edges, face.C, face.A);
                faces.RemoveAt(i);
            }
        }

        foreach (var (a, b) in edges)
        {
            faces.Add(new Face(a, b, newIndex, vertices.ToArray()));
        }
    }

    private static void AddEdge(List<(int, int)> edges, int a, int b)
    {
        for (int i = edges.Count - 1; i >= 0; i--)
        {
            if ((edges[i].Item1 == b && edges[i].Item2 == a) ||
                (edges[i].Item1 == a && edges[i].Item2 == b))
            {
                edges.RemoveAt(i);
                return;
            }
        }
        edges.Add((a, b));
    }

    private static Vector3 Support(ICollisionShape shapeA, ICollisionShape shapeB, Vector3 direction)
    {
        Vector3 supportA = shapeA.Support(direction);
        Vector3 supportB = shapeB.Support(-direction);
        return supportA - supportB;
    }
}

/// <summary>
/// 穿透信息
/// </summary>
public struct PenetrationInfo
{
    public static PenetrationInfo NoCollision => new() { HasCollision = false };

    /// <summary>碰撞法向量（从 B 指向 A）</summary>
    public Vector3 Normal;

    /// <summary>穿透深度</summary>
    public float Depth;

    /// <summary>是否发生碰撞</summary>
    public bool HasCollision;
}
