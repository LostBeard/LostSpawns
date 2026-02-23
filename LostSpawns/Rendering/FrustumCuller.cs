using System.Numerics;

namespace LostSpawns.Rendering;

/// <summary>
/// Extracts frustum planes from a View-Projection matrix and tests
/// axis-aligned bounding boxes for visibility. Uses the Gribb-Hartmann
/// method for plane extraction.
/// </summary>
public static class FrustumCuller
{
    /// <summary>
    /// Frustum planes: Left, Right, Bottom, Top, Near, Far.
    /// Each Vector4 is (A, B, C, D) where Ax + By + Cz + D >= 0 means inside.
    /// </summary>
    public struct Frustum
    {
        public Vector4 Left, Right, Bottom, Top, Near, Far;
    }

    /// <summary>
    /// Extract 6 frustum planes from a combined View*Projection matrix.
    /// System.Numerics uses row-vector convention (v * M), so planes are
    /// extracted from rows.
    /// </summary>
    public static Frustum ExtractPlanes(Matrix4x4 vp)
    {
        Frustum f;

        // Left:   row4 + row1
        f.Left = new Vector4(
            vp.M14 + vp.M11,
            vp.M24 + vp.M21,
            vp.M34 + vp.M31,
            vp.M44 + vp.M41);

        // Right:  row4 - row1
        f.Right = new Vector4(
            vp.M14 - vp.M11,
            vp.M24 - vp.M21,
            vp.M34 - vp.M31,
            vp.M44 - vp.M41);

        // Bottom: row4 + row2
        f.Bottom = new Vector4(
            vp.M14 + vp.M12,
            vp.M24 + vp.M22,
            vp.M34 + vp.M32,
            vp.M44 + vp.M42);

        // Top:    row4 - row2
        f.Top = new Vector4(
            vp.M14 - vp.M12,
            vp.M24 - vp.M22,
            vp.M34 - vp.M32,
            vp.M44 - vp.M42);

        // Near:   row4 + row3
        f.Near = new Vector4(
            vp.M14 + vp.M13,
            vp.M24 + vp.M23,
            vp.M34 + vp.M33,
            vp.M44 + vp.M43);

        // Far:    row4 - row3
        f.Far = new Vector4(
            vp.M14 - vp.M13,
            vp.M24 - vp.M23,
            vp.M34 - vp.M33,
            vp.M44 - vp.M43);

        // Normalize planes
        f.Left = NormalizePlane(f.Left);
        f.Right = NormalizePlane(f.Right);
        f.Bottom = NormalizePlane(f.Bottom);
        f.Top = NormalizePlane(f.Top);
        f.Near = NormalizePlane(f.Near);
        f.Far = NormalizePlane(f.Far);

        return f;
    }

    /// <summary>
    /// Test whether an axis-aligned bounding box is at least partially visible
    /// in the frustum. Returns true if visible, false if fully culled.
    /// </summary>
    public static bool IsBoxVisible(in Frustum frustum, Vector3 min, Vector3 max)
    {
        // Test against each plane: find the corner most aligned with the plane normal.
        // If that "positive" corner is behind the plane, the entire box is outside.
        if (!TestPlane(frustum.Left, min, max)) return false;
        if (!TestPlane(frustum.Right, min, max)) return false;
        if (!TestPlane(frustum.Bottom, min, max)) return false;
        if (!TestPlane(frustum.Top, min, max)) return false;
        if (!TestPlane(frustum.Near, min, max)) return false;
        if (!TestPlane(frustum.Far, min, max)) return false;
        return true;
    }

    private static bool TestPlane(Vector4 plane, Vector3 min, Vector3 max)
    {
        // Pick the corner of the AABB that is most in the direction of the plane normal
        float px = plane.X >= 0 ? max.X : min.X;
        float py = plane.Y >= 0 ? max.Y : min.Y;
        float pz = plane.Z >= 0 ? max.Z : min.Z;

        // If the positive corner is behind the plane, box is fully outside
        return plane.X * px + plane.Y * py + plane.Z * pz + plane.W >= 0;
    }

    private static Vector4 NormalizePlane(Vector4 plane)
    {
        float len = MathF.Sqrt(plane.X * plane.X + plane.Y * plane.Y + plane.Z * plane.Z);
        if (len < 1e-8f) return plane;
        return plane / len;
    }
}
