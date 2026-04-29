namespace GPGems.Core.Math;

/// <summary>
/// 弹簧阻尼平滑
/// 用于相机跟随、动画过渡等平滑运动
/// </summary>
public static class SpringDamping
{
    /// <summary>
    /// 临界阻尼弹簧平滑（无振荡）
    /// </summary>
    /// <param name="current">当前值</param>
    /// <param name="target">目标值</param>
    /// <param name="velocity">当前速度（输入输出）</param>
    /// <param name="deltaTime">时间步长</param>
    /// <param name="smoothTime">平滑时间（秒），越小越快</param>
    public static float SmoothDamp(float current, float target, ref float velocity, float deltaTime, float smoothTime = 0.1f)
    {
        smoothTime = MathF.Max(0.0001f, smoothTime);
        float omega = 2f / smoothTime;

        float x = omega * deltaTime;
        float exp = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);

        float delta = current - target;
        float temp = (velocity + omega * delta) * deltaTime;

        velocity = (velocity - omega * temp) * exp;
        return target + (delta + temp) * exp;
    }

    /// <summary>向量版弹簧平滑</summary>
    public static Vector3 SmoothDamp(Vector3 current, Vector3 target, ref Vector3 velocity, float deltaTime, float smoothTime = 0.1f)
    {
        smoothTime = MathF.Max(0.0001f, smoothTime);
        float omega = 2f / smoothTime;

        float x = omega * deltaTime;
        float exp = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);

        Vector3 delta = current - target;
        Vector3 temp = (velocity + delta * omega) * deltaTime;

        velocity = (velocity - temp * omega) * exp;
        return target + (delta + temp) * exp;
    }

    /// <summary>四元数版弹簧平滑</summary>
    public static Quaternion SmoothDamp(Quaternion current, Quaternion target, ref Vector3 angularVelocity, float deltaTime, float smoothTime = 0.1f)
    {
        Vector3 currentEuler = ToEuler(current);
        Vector3 targetEuler = ToEuler(target);

        Vector3 smoothed = SmoothDamp(currentEuler, targetEuler, ref angularVelocity, deltaTime, smoothTime);
        return Quaternion.FromEuler(smoothed.X, smoothed.Y, smoothed.Z);
    }

    private static Vector3 ToEuler(Quaternion q)
    {
        float sinp = 2 * (q.W * q.Y - q.Z * q.X);
        if (MathF.Abs(sinp) >= 1)
        {
            return new Vector3(
                MathF.CopySign(MathF.PI / 2, sinp),
                MathF.Atan2(-q.X * q.Z + q.W * q.Y, 0.5f - q.Y * q.Y - q.Z * q.Z),
                0
            );
        }

        return new Vector3(
            MathF.Asin(sinp),
            MathF.Atan2(q.X * q.Z + q.W * q.Y, 0.5f - q.Y * q.Y - q.X * q.X),
            MathF.Atan2(q.X * q.Y + q.W * q.Z, 0.5f - q.Z * q.Z - q.X * q.X)
        );
    }
}
