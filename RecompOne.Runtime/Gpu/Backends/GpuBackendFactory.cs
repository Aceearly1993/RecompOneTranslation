using Silk.NET.OpenGL;

namespace RecompOne.Runtime.Hle;
//the idea is to drop gl45 is gl33 is stable enought, in the future maybe add vulkan and dx backend?
public enum GlBackendKind { Auto, Gl45, Gl33 }

public static class GpuBackendFactory //fkn hate these factories
{
    public static GlBackendKind Selected { get; private set; }

    public static IGpuBackend Create(GL gl, GlBackendKind requested)
    {
        GlBackendKind kind = requested == GlBackendKind.Auto
            ? (Supports45(gl) ? GlBackendKind.Gl45 : GlBackendKind.Gl33)
            : requested;

        Selected = kind;
        IGlVram vram = kind == GlBackendKind.Gl45 ? new Gl45Vram(gl) : new Gl33Vram(gl);
        Console.WriteLine($"[Gpu] backend: {kind}");
        return new GlCore(gl, vram);
    }

    static bool Supports45(GL gl)
    {
        try
        {
            int major = gl.GetInteger(GLEnum.MajorVersion);
            int minor = gl.GetInteger(GLEnum.MinorVersion);
            return major > 4 || (major == 4 && minor >= 5);
        }
        catch { return false; }
    }

    public static GlBackendKind Parse(string? s) => s?.ToLowerInvariant() switch
    {
        "gl45" => GlBackendKind.Gl45,
        "gl33" => GlBackendKind.Gl33,
        _ => GlBackendKind.Auto,
    };
}
