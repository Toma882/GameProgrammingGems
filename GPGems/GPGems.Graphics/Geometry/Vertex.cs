using System.Numerics;
using GPGems.Core.Math;

namespace GPGems.Graphics.Geometry;

/// <summary>
/// 顶点
/// </summary>
public struct Vertex
{
    public Vector3 Position;
    public Vector3 Normal;
    public Vector2 UV;

    public Vertex(Vector3 position)
    {
        Position = position;
        Normal = Vector3.UnitY;
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

    public static Vertex operator +(Vertex a, Vertex b) =>
        new Vertex(a.Position + b.Position, a.Normal + b.Normal, a.UV + b.UV);

    public static Vertex operator -(Vertex a, Vertex b) =>
        new Vertex(a.Position - b.Position, a.Normal - b.Normal, a.UV - b.UV);

    public static Vertex operator *(Vertex v, float s) =>
        new Vertex(v.Position * s, v.Normal * s, v.UV * s);

    public static Vertex operator *(float s, Vertex v) => v * s;
}
