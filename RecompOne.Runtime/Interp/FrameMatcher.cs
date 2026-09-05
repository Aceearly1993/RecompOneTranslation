using RecompOne.Runtime.Hle;

namespace RecompOne.Runtime.Interp;

internal sealed class FrameMatcher
{
    private const ulong HashSeed = 0xCBF29CE484222325ul;
    private const ulong HashPrime = 0x100000001B3ul;
    
    private const float Certain = 4f;
    private const float Separation = 4f;
    
    private readonly Dictionary<ulong, List<int>> _buckets = new();
    private readonly List<List<int>> _pool = [];
    private bool[] _used = [];
    
    public int Matched { get; private set; }
    
    public int Total { get; private set; }
    
    public float Rate => Total == 0 ? 0f : Matched / (float)Total;
    
    public static ulong KeyOf(in HleVertex a, in HleVertex b, in HleVertex c, in PrimFlags f)
    {
        var hash = HashSeed;
        
        Mix(ref hash, f.TPage);
        Mix(ref hash, f.Clut);
        Mix(ref hash, (uint)(f.Textured ? 1 : 0) | (uint)(f.Gouraud ? 2 : 0) | (uint)(f.SemiTrans ? 4 : 0));
        
        Mix(ref hash, Uv(in a));
        Mix(ref hash, Uv(in b));
        Mix(ref hash, Uv(in c));
        
        return hash;
    }
    
    public void Match(FrameGraph current, FrameGraph previous, float radius)
    {
        Release();
        
        Matched = 0;
        Total = current.Tris.Count;
        
        if (_used.Length < previous.Tris.Count) _used = new bool[previous.Tris.Count];
        Array.Clear(_used, 0, previous.Tris.Count);
        
        for (var i = 0; i < previous.Tris.Count; i++)
        {
            var key = previous.Tris[i].Key;
            if (!_buckets.TryGetValue(key, out var bucket)) _buckets[key] = bucket = Rent();
            bucket.Add(i);
        }
        
        var limit = radius * radius;
        
        for (var i = 0; i < current.Tris.Count; i++)
        {
            var tri = current.Tris[i];
            tri.Match = Nearest(previous, in tri, limit);
            
            if (tri.Match >= 0)
            {
                _used[tri.Match] = true;
                Matched++;
            }
            
            current.Tris[i] = tri;
        }
    }
    
    private int Nearest(FrameGraph previous, in TriRecord tri, float limit)
    {
        if (!_buckets.TryGetValue(tri.Key, out var bucket) || bucket.Count == 0) return -1;
        
        var best = -1;
        var bestDistance = float.MaxValue;
        var runnerUp = float.MaxValue;
        
        foreach (var index in bucket)
        {
            if (_used[index]) continue;
            
            var distance = Distance(previous.Tris[index], in tri);
            
            if (distance < bestDistance)
            {
                runnerUp = bestDistance;
                bestDistance = distance;
                best = index;
                continue;
            }
            
            if (distance < runnerUp) runnerUp = distance;
        }
        
        if (best < 0 || bestDistance > limit) return -1;
        if (bestDistance <= Certain) return best;
        
        return runnerUp >= bestDistance * Separation ? best : -1;
    }
    
    private static float Distance(in TriRecord from, in TriRecord to)
    {
        var dx = to.A.X - from.A.X;
        var dy = to.A.Y - from.A.Y;
        var worst = dx * dx + dy * dy;
        
        dx = to.B.X - from.B.X;
        dy = to.B.Y - from.B.Y;
        worst = Math.Max(worst, dx * dx + dy * dy);
        
        dx = to.C.X - from.C.X;
        dy = to.C.Y - from.C.Y;
        return Math.Max(worst, dx * dx + dy * dy);
    }
    
    private void Release()
    {
        foreach (var bucket in _buckets.Values)
        {
            bucket.Clear();
            _pool.Add(bucket);
        }
        
        _buckets.Clear();
    }
    
    private List<int> Rent()
    {
        if (_pool.Count == 0) return [];
        
        var bucket = _pool[^1];
        _pool.RemoveAt(_pool.Count - 1);
        return bucket;
    }
    
    private static uint Uv(in HleVertex v)
    {
        return (uint)((int)v.U | ((int)v.V << 8));
    }
    
    private static void Mix(ref ulong hash, uint value)
    {
        hash = (hash ^ value) * HashPrime;
    }
}
