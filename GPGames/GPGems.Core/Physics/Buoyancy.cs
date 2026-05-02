using System.Numerics;
using GPGems.Core.Math;
using GPGems.Core.Geometry;

namespace GPGems.Core.Physics;

/// <summary>
/// 精确浮力计算
/// 基于 Game Programming Gems 2 Chapter 2.5
/// 模拟物体在流体中的浮力效应
/// </summary>
public class BuoyancySolver
{
    public float WaterDensity { get; set; } = 1000f;
    public float Gravity { get; set; } = 9.81f;
    public float WaterLevel { get; set; } = 0f;
    public float LinearDrag { get; set; } = 0.5f;
    public float AngularDrag { get; set; } = 0.3f;

    private struct BuoyancyVertex
    {
        public Vector3 Position;
        public float Depth;
    }

    /// <summary>
    /// 计算并施加浮力到刚体
    /// </summary>
    public void ApplyBuoyancy(RigidBody body, Vector3[] localVerts, float totalVolume)
    {
        var submerged = GetSubmergedVertices(body, localVerts);
        if (submerged.Length == 0)
            return;

        if (submerged.Length > 3)
        {
            var submergedVolume = CalculateSubmergedVolume(submerged);
            var buoyancyForce = Vector3.UnitY * WaterDensity * Gravity * submergedVolume;
            var center = CalculateCenterOfBuoyancy(submerged);
            body.ApplyForceAtWorldPosition(buoyancyForce, center);
        }

        if (submerged.Length == localVerts.Length)
        {
            ApplyDrag(body, localVerts, 1f);
        }
        else
        {
            float submergedRatio = (float)submerged.Length / localVerts.Length;
            ApplyDrag(body, submerged.Select(v => v.Position).ToArray(), submergedRatio);
        }
    }

    private BuoyancyVertex[] GetSubmergedVertices(RigidBody body, Vector3[] localVerts)
    {
        var result = new List<BuoyancyVertex>();

        foreach (var local in localVerts)
        {
            var worldPos = body.Position + body.Orientation.Rotate(local);
            float depth = WaterLevel - worldPos.Y;

            if (depth > 0)
            {
                result.Add(new BuoyancyVertex { Position = worldPos, Depth = depth });
            }
        }

        return result.ToArray();
    }

    private float CalculateSubmergedVolume(BuoyancyVertex[] vertices)
    {
        if (vertices.Length < 4)
            return 0;

        Vector3 center = Vector3.Zero;
        foreach (var v in vertices)
            center += v.Position;
        center /= vertices.Length;

        float volume = 0;
        for (int i = 0; i < vertices.Length; i++)
        {
            for (int j = i + 1; j < vertices.Length; j++)
            {
                for (int k = j + 1; k < vertices.Length; k++)
                {
                    volume += TetrahedronVolume(
                        center,
                        vertices[i].Position,
                        vertices[j].Position,
                        vertices[k].Position
                    );
                }
            }
        }

        return MathF.Abs(volume);
    }

    private float TetrahedronVolume(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        var ab = b - a;
        var ac = c - a;
        var ad = d - a;
        return Vector3.Dot(ab, Vector3.Cross(ac, ad)) / 6f;
    }

    private Vector3 CalculateCenterOfBuoyancy(BuoyancyVertex[] vertices)
    {
        Vector3 center = Vector3.Zero;
        float totalWeight = 0;

        foreach (var v in vertices)
        {
            float weight = v.Depth * v.Depth;
            center += v.Position * weight;
            totalWeight += weight;
        }

        return totalWeight > 0 ? center / totalWeight : Vector3.Zero;
    }

    private void ApplyDrag(RigidBody body, Vector3[] worldVerts, float submergedRatio)
    {
        foreach (var pos in worldVerts)
        {
            var relativeVel = body.LinearVelocity + Vector3.Cross(body.AngularVelocity, pos - body.Position);
            var dragForce = -relativeVel * LinearDrag * submergedRatio;
            body.ApplyForceAtWorldPosition(dragForce, pos);

            var angularDrag = -body.AngularVelocity * AngularDrag * submergedRatio;
            body.ApplyForce(angularDrag);
        }
    }
}
