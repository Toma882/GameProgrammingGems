/* Copyright (C) Steven Woodcock, 2000.
 * Ported to C# from Game Programming Gems 1
 *
 * NOTE: Custom Vector2/Vector3 have been replaced with System.Numerics.Vector2/Vector3
 * for SIMD hardware acceleration. Extension methods for game development convenience
 * are in VectorExtensions.cs.
 */

using System.Numerics;

using System.Numerics;
namespace GPGems.Core.Math;

/// <summary>
/// Import aliases for backwards compatibility
/// </summary>
internal static class VectorAliases
{
    // This file is intentionally minimal.
    // Use System.Numerics.Vector2/Vector3 directly in new code.
}
