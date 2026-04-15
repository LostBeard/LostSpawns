using System.Numerics;
using SpawnDev.VoxelEngine.Rendering;

namespace LostSpawns.Rendering;

/// <summary>
/// First-person camera with yaw/pitch mouse look and WASD movement.
/// Uses System.Numerics for matrix math.
/// </summary>
public class Camera
{
    public Vector3 Position { get; set; } = new(8f, 8f, 20f);
    public float Yaw { get; set; } = -90f;   // degrees, -90 = looking along -Z
    public float Pitch { get; set; } = -15f;  // degrees

    public float MovementSpeed { get; set; } = 32f;
    public float MouseSensitivity { get; set; } = 0.15f;

    /// <summary>Forward direction (unit vector) derived from yaw and pitch.</summary>
    public Vector3 Front
    {
        get
        {
            float yawRad = MathF.PI / 180f * Yaw;
            float pitchRad = MathF.PI / 180f * Pitch;
            return Vector3.Normalize(new Vector3(
                MathF.Cos(yawRad) * MathF.Cos(pitchRad),
                MathF.Sin(pitchRad),
                MathF.Sin(yawRad) * MathF.Cos(pitchRad)
            ));
        }
    }

    public Vector3 Right => Vector3.Normalize(Vector3.Cross(Front, Vector3.UnitY));
    public Vector3 Up => Vector3.Normalize(Vector3.Cross(Right, Front));

    /// <summary>
    /// Process raw mouse movement deltas (from InputService).
    /// </summary>
    public void ProcessMouseMovement(float dx, float dy)
    {
        Yaw += dx * MouseSensitivity;
        Pitch -= dy * MouseSensitivity;  // inverted Y
        Pitch = Math.Clamp(Pitch, -89f, 89f);
    }

    /// <summary>
    /// Process WASD keyboard movement.
    /// </summary>
    public void ProcessKeyboard(HashSet<string> keysDown, float deltaTime)
    {
        float velocity = MovementSpeed * deltaTime;
        // Flatten forward/right to XZ plane for FPS-style movement
        var flatFront = Vector3.Normalize(new Vector3(Front.X, 0, Front.Z));
        var flatRight = Vector3.Normalize(Vector3.Cross(flatFront, Vector3.UnitY));

        if (keysDown.Contains("KeyW")) Position += flatFront * velocity;
        if (keysDown.Contains("KeyS")) Position -= flatFront * velocity;
        if (keysDown.Contains("KeyA")) Position -= flatRight * velocity;
        if (keysDown.Contains("KeyD")) Position += flatRight * velocity;
        if (keysDown.Contains("Space")) Position += Vector3.UnitY * velocity;
        if (keysDown.Contains("ShiftLeft")) Position -= Vector3.UnitY * velocity;
    }

    /// <summary>Returns 4×4 view matrix (column-major, ready for GPU upload).</summary>
    public Matrix4x4 GetViewMatrix()
    {
        return Matrix4x4.CreateLookAt(Position, Position + Front, Vector3.UnitY);
    }

    /// <summary>Returns reversed-Z infinite perspective projection for VertexPullPipeline.</summary>
    public Matrix4x4 GetProjectionMatrix(float aspectRatio, float fovDegrees = 70f, float near = 0.1f)
    {
        float fovRad = fovDegrees * MathF.PI / 180f;
        return ReversedZHelper.CreateInfinitePerspective(fovRad, aspectRatio, near);
    }

    /// <summary>Returns the combined View*Projection matrix (for frustum culling).</summary>
    public Matrix4x4 GetVpMatrix(float aspectRatio, float fovDegrees = 70f)
    {
        return GetViewMatrix() * GetProjectionMatrix(aspectRatio, fovDegrees);
    }

    /// <summary>Returns the combined MVP matrix as a float[16] column-major array for GPU upload.</summary>
    public float[] GetMvpArray(float aspectRatio, float fovDegrees = 70f)
    {
        var vp = GetViewMatrix() * GetProjectionMatrix(aspectRatio, fovDegrees);
        return MatrixToArray(vp);
    }

    /// <summary>Writes the MVP matrix into an existing float[16] buffer (zero-allocation).</summary>
    public void WriteMvp(float[] buffer, float aspectRatio, float fovDegrees = 70f)
    {
        var vp = GetViewMatrix() * GetProjectionMatrix(aspectRatio, fovDegrees);
        WriteMatrixToArray(vp, buffer);
    }

    private static float[] MatrixToArray(Matrix4x4 m)
    {
        // System.Numerics stores row-major (row-vector convention: v * M).
        // WGSL reads mat4x4<f32> as column-major (column-vector convention: M * v).
        // Uploading raw row-major bytes to column-major storage naturally transposes
        // the matrix, which is exactly the convention change we need.
        // Do NOT manually transpose — that would double-transpose and break it.
        return new[]
        {
            m.M11, m.M12, m.M13, m.M14,
            m.M21, m.M22, m.M23, m.M24,
            m.M31, m.M32, m.M33, m.M34,
            m.M41, m.M42, m.M43, m.M44,
        };
    }

    private static void WriteMatrixToArray(Matrix4x4 m, float[] buf)
    {
        buf[0]  = m.M11; buf[1]  = m.M12; buf[2]  = m.M13; buf[3]  = m.M14;
        buf[4]  = m.M21; buf[5]  = m.M22; buf[6]  = m.M23; buf[7]  = m.M24;
        buf[8]  = m.M31; buf[9]  = m.M32; buf[10] = m.M33; buf[11] = m.M34;
        buf[12] = m.M41; buf[13] = m.M42; buf[14] = m.M43; buf[15] = m.M44;
    }
}
