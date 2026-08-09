using Silk.NET.OpenGL;

namespace RecompOne.Runtime.Hle;
//the idea is to drop gl45 is gl33 is stable enought, in the future maybe add vulkan and dx backend?
public enum GlBackendKind { Auto, Gl45, Gl33 }

public static class GpuBackendFactory //fkn hate these factories
{
    public static GlBackendKind Selected { get; private set; }

    public static IGpuBackend Create(GL gl, GlBackendKind requested)
    {
        bool has45 = Supports45(gl);
        GlBackendKind kind = requested == GlBackendKind.Auto //if gl45 is not availble fallback to 33 so that it works 
            ? (has45 ? GlBackendKind.Gl45 : GlBackendKind.Gl33)
            : requested;

        if (kind == GlBackendKind.Gl45 && !has45)
        {
            Console.WriteLine("[Gpu] gl45 requested but the support by your gpu is below 4.5, falling back to gl33");
            kind = GlBackendKind.Gl33;
        }

        Selected = kind;
        IGlVram vram = kind == GlBackendKind.Gl45 ? new Gl45Vram(gl) : new Gl33Vram(gl);
        Console.WriteLine($"[Gpu] backend: {kind}");
        return new GlCore(gl, vram);
    }

    static bool Supports45(GL gl)
    {
        int major = 0, minor = 0;
        try
        {
            major = gl.GetInteger(GLEnum.MajorVersion);
            minor = gl.GetInteger(GLEnum.MinorVersion);
        }
        catch (Exception e)
        {
            Console.WriteLine($"[Gpu] could not read the context version: {e.Message}");
        }

        string version = Str(gl, StringName.Version);
        string renderer = Str(gl, StringName.Renderer);
        Console.WriteLine($"[Gpu] context: {major}.{minor} ({version}) on {renderer}");

        if (major > 4 || (major == 4 && minor >= 5)) return true;

        bool barrier = HasExtension(gl, "GL_ARB_texture_barrier");
        bool copyImage = HasExtension(gl, "GL_ARB_copy_image");
        if (barrier && copyImage)
        {
            Console.WriteLine("[Gpu] context is below 4.5 but exposes texture barrier and copy image");
            return true;
        }

        Console.WriteLine($"[Gpu] gl45 unavailable (texture barrier: {barrier}, copy image: {copyImage})");
        return false;
    }

    static bool HasExtension(GL gl, string name)
    {
        try
        {
            int count = gl.GetInteger(GLEnum.NumExtensions);
            for (uint i = 0; i < count; i++)
                if (gl.GetStringS(StringName.Extensions, i) == name) return true;
        }
        catch { }
        return false;
    }

    static string Str(GL gl, StringName name)
    {
        try { return gl.GetStringS(name) ?? "?"; }
        catch { return "?"; }
    }

    public static GlBackendKind Parse(string? s) => s?.ToLowerInvariant() switch
    {
        "gl45" => GlBackendKind.Gl45,
        "gl33" => GlBackendKind.Gl33,
        _ => GlBackendKind.Auto,
    };
}
