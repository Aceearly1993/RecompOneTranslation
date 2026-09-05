using RecompOne.Runtime.Hle;

namespace RecompOne.Runtime.Interp;

internal sealed class VertexMap
{
    private readonly Dictionary<long, HleVertex> _moved = new();
    public int Count => _moved.Count;
    
    public void Build(FrameGraph current, FrameGraph previous)
    {
        _moved.Clear();
        
        foreach (var tri in current.Tris)
        {
            if (tri.Match < 0 || tri.Match >= previous.Tris.Count) continue;
            
            var from = previous.Tris[tri.Match];
            
            Pair(in tri.A, in from.A);
            Pair(in tri.B, in from.B);
            Pair(in tri.C, in from.C);
        }
    }
    
    /// <summary>
    /// resolves every pairing once, so replaying a display frame only has to
    /// interpolate instead of hashing each vertex again
    /// </summary>
    public void Resolve(FrameGraph current)
    {
        current.Motion.Clear();
        
        foreach (var tri in current.Tris)
        {
            var motion = default(TriMotion);
            
            if (Find(in tri.A, out var a))
            {
                motion.AX = a.X;
                motion.AY = a.Y;
                motion.AZ = a.Z;
                motion.Moved |= 1;
            }
            
            if (Find(in tri.B, out var b))
            {
                motion.BX = b.X;
                motion.BY = b.Y;
                motion.BZ = b.Z;
                motion.Moved |= 2;
            }
            
            if (Find(in tri.C, out var c))
            {
                motion.CX = c.X;
                motion.CY = c.Y;
                motion.CZ = c.Z;
                motion.Moved |= 4;
            }
            
            current.Motion.Add(motion);
        }
    }
    
    public bool Moves(in HleVertex current)
    {
        return _moved.ContainsKey(KeyOf(in current));
    }
    
    private bool Find(in HleVertex current, out HleVertex previous)
    {
        return _moved.TryGetValue(KeyOf(in current), out previous);
    }
    
    private void Pair(in HleVertex current, in HleVertex previous)
    {
        if (!current.HasGteZ || !previous.HasGteZ) return;
        
        _moved.TryAdd(KeyOf(in current), previous);
    }
    
    private static long KeyOf(in HleVertex v) =>  ((long)BitConverter.SingleToInt32Bits(v.X) << 32) | (uint)BitConverter.SingleToInt32Bits(v.Y);
    
    
    public static HleVertex Blend(in HleVertex to, float fromX, float fromY, float fromZ, float t)
    {
        var v = to;
        
        v.X = fromX + (to.X - fromX) * t;
        v.Y = fromY + (to.Y - fromY) * t;
        
        if (fromZ > 0f && to.Z > 0f)
        {
            var inverse = 1f / fromZ + (1f / to.Z - 1f / fromZ) * t;
            if (inverse > 0f) v.Z = 1f / inverse;
        }
        
        return v;
    }
}
