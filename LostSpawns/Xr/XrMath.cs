using System.Numerics;

namespace LostSpawns.Xr;

/// <summary>
/// Where a headset's room sits in the world, and the views that follow from it. WebXR poses are in the room's local-floor space (metres, y up, the headset starts near (0, 1.7, 0)); the world is
/// in blocks. A block is a metre here, so a person is a little shorter than the player. Row-vector convention throughout: <c>pWorld = pRoom * worldFromRoom</c>.
/// </summary>
public static class XrMath
{
    /// <summary>A WebXR matrix (16 floats, column-major, for column vectors) as a System.Numerics matrix (row vectors): the floats go in row by row as they come.</summary>
    public static Matrix4x4 FromWebXrMatrix(float[] m) => new(
        m[0], m[1], m[2], m[3],
        m[4], m[5], m[6], m[7],
        m[8], m[9], m[10], m[11],
        m[12], m[13], m[14], m[15]);

    /// <summary>
    /// The room's place in the world at the start: the head is where the fly camera was (<paramref name="cameraPosition"/>) and looks the way it looked (<paramref name="cameraForward"/>,
    /// only its heading counts: the room stays level).
    /// </summary>
    public static Matrix4x4 WorldFromRoom(Vector3 headPosition, Quaternion headOrientation, Vector3 cameraPosition, Vector3 cameraForward)
    {
        float turn = Heading(cameraForward) - Heading(Vector3.Transform(-Vector3.UnitZ, headOrientation));
        return Matrix4x4.CreateTranslation(-headPosition) * Matrix4x4.CreateRotationY(turn) * Matrix4x4.CreateTranslation(cameraPosition);
    }

    // How far to the left of -Z a horizontal direction points (what Matrix4x4.CreateRotationY adds to it)
    private static float Heading(Vector3 v) => MathF.Atan2(-v.X, -v.Z);

    /// <summary>The eye's view of the world: world -> room (undo the room's place) -> eye (the view WebXR gave for the room).</summary>
    public static Matrix4x4 EyeView(Matrix4x4 roomView, Matrix4x4 worldFromRoom)
    {
        Matrix4x4.Invert(worldFromRoom, out var roomFromWorld);
        return roomFromWorld * roomView;
    }

    /// <summary>Where an eye is in the world, from the view WebXR gave it in the room.</summary>
    public static Vector3 EyeInWorld(Matrix4x4 roomView, Matrix4x4 worldFromRoom)
    {
        Matrix4x4.Invert(roomView, out var eyeToRoom);
        return Vector3.Transform(eyeToRoom.Translation, worldFromRoom);
    }

    /// <summary>A view matrix (row-vector: world -> eye) from the pose of an XRRigidTransform (position and orientation of the eye in the room).</summary>
    public static Matrix4x4 ViewFromPose(Vector3 position, Quaternion orientation)
        => Matrix4x4.CreateTranslation(-position) * Matrix4x4.CreateFromQuaternion(Quaternion.Conjugate(orientation));

    /// <summary>The fly camera's yaw (degrees, 0 = +X) and pitch of a world direction.</summary>
    public static (float Yaw, float Pitch) AnglesOf(Vector3 direction)
    {
        var d = Vector3.Normalize(direction);
        return (MathF.Atan2(d.Z, d.X) * 180f / MathF.PI, MathF.Asin(Math.Clamp(d.Y, -1f, 1f)) * 180f / MathF.PI);
    }
}
