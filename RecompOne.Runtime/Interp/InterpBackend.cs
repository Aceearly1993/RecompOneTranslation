using System.Runtime.InteropServices;
using RecompOne.Runtime.Hle;

namespace RecompOne.Runtime.Interp;

public sealed class InterpBackend : IGpuBackend
{
    private const float MaxDelta = 96f; //tbh this is mostly arbitrary, 96 seens to work fine enough for all cases, note to self: if something break look at this first
    
    private readonly IGpuBackend _inner;
    private readonly InterpClock _clock = new();
    private readonly System.Diagnostics.Stopwatch _watch = System.Diagnostics.Stopwatch.StartNew();
    
    private double _frameStartMs;
    private double _budgetMs;
    private double _renderedMs;
    private int _rendered;
    private int _source;
    private readonly FrameMatcher _matcher = new();
    private readonly VertexMap _map = new();
    
    private readonly Lock _gate = new();
    private readonly Stack<FrameGraph> _free = new();
    
    private FrameGraph _recording = new();
    private FrameGraph? _ready;
    private FrameGraph _current = new();
    private FrameGraph _previous = new();
    
    private bool _active;
    private uint _lastAddress;
    private int _addressSub;
    private bool _interpolating;
    private int _frames = 1;
    
    public InterpBackend(IGpuBackend inner)
    {
        _inner = inner;
        _active = inner.Ready;
        Interp.Backend = this;
    }
    
    public bool Ready => _inner.Ready;
    
    public int SourceRate => _source;
    
    public float MatchRate => _matcher.Rate;
    
    public void SetDrawEnv(in HleDrawEnv env)
    {
        if (!_active)
        {
            _inner.SetDrawEnv(env);
            return;
        }
        _recording.Envs.Add(env);
        _recording.Add(GraphOp.DrawEnv, _recording.Envs.Count - 1);
    }
    
    public void DrawTri(in HleVertex a, in HleVertex b, in HleVertex c, in PrimFlags f)
    {
        if (!_active)
        {
            _inner.DrawTri(in a, in b, in c, in f);
            return;
        }
        
        var offsetX = (float)(Runtime.Gpu?.DrawOffsetX ?? 0);
        var offsetY = (float)(Runtime.Gpu?.DrawOffsetY ?? 0);
        
        var address = Runtime.Gpu?.FifoBase ?? 0u;
        if (address != 0u && address == _lastAddress) _addressSub++;
        else _addressSub = 0;
        _lastAddress = address;
        
        _recording.Tris.Add(new TriRecord
        {
            A = Detach(in a, offsetX, offsetY),
            B = Detach(in b, offsetX, offsetY),
            C = Detach(in c, offsetX, offsetY),
            Flags = f,
            Key = FrameMatcher.KeyOf(in a, in b, in c, in f),
            Address = address,
            Sub = _addressSub,
            Match = -1,
            OffsetX = offsetX,
            OffsetY = offsetY
        });
        
        _recording.Add(GraphOp.Tri, _recording.Tris.Count - 1);
    }
    
    public void DrawRect(in HleRect r, in PrimFlags f)
    {
        if (!_active)
        {
            _inner.DrawRect(in r, in f);
            return;
        }
        
        _recording.Rects.Add(new RectRecord { Rect = r, Flags = f });
        _recording.Add(GraphOp.Rect, _recording.Rects.Count - 1);
    }
    
    public void DrawLine(in HleVertex a, in HleVertex b, in PrimFlags f)
    {
        if (!_active)
        {
            _inner.DrawLine(in a, in b, in f);
            return;
        }
        
        _recording.Lines.Add(new LineRecord { A = a, B = b, Flags = f });
        _recording.Add(GraphOp.Line, _recording.Lines.Count - 1);
    }
    
    public void FillRect(int x, int y, int w, int h, ushort color15)
    {
        if (!_active)
        {
            _inner.FillRect(x, y, w, h, color15);
            return;
        }
        
        _recording.Fills.Add(new FillRecord { X = x, Y = y, W = w, H = h, Color = color15 });
        _recording.Add(GraphOp.Fill, _recording.Fills.Count - 1);
    }
    
    public void CopyVram(int sx, int sy, int dx, int dy, int w, int h)
    {
        if (!_active)
        {
            _inner.CopyVram(sx, sy, dx, dy, w, h);
            return;
        }
        
        _recording.Copies.Add(new CopyRecord { Sx = sx, Sy = sy, Dx = dx, Dy = dy, W = w, H = h });
        _recording.Add(GraphOp.CopyVram, _recording.Copies.Count - 1);
    }
    
    public void WriteVram(int x, int y, int w, int h, ReadOnlySpan<ushort> px)
    {
        if (!_active)
        {
            _inner.WriteVram(x, y, w, h, px);
            return;
        }
        
        var offset = _recording.AddPixels(px);
        _recording.Writes.Add(new WriteRecord { X = x, Y = y, W = w, H = h, Offset = offset, Length = px.Length });
        _recording.Add(GraphOp.WriteVram, _recording.Writes.Count - 1);
    }
    
    public void ReadVram(int x, int y, int w, int h, Span<ushort> px)
    {
        Settle();
        
        _inner.ReadVram(x, y, w, h, px);
    }
    
    public int RegisterImage(ReadOnlySpan<byte> rgba, int width, int height)
    {
        return _inner.RegisterImage(rgba, width, height);
    }
    
    public void Flush()
    {
        Settle();
        _inner.Flush();
    }
    
    public void Present(in HleDispEnv disp)
    {
        _inner.Present(in disp);
    }
    
    public double PaceMs { get; private set; }
    
    public void Publish()
    {
        _lastAddress = 0u;
        _addressSub = 0;

        if (!_active) return;
        
        lock (_gate)
        {
            if (_ready != null) Recycle(_ready);
            
            _ready = _recording;
            _recording = _free.Count > 0 ? _free.Pop() : new FrameGraph();
            _recording.Clear();
        }
    }
    
    public bool Acquire()
    {
        if (!_active) return false;
        
        lock (_gate)
        {
            if (_ready == null) return false;
            
            Recycle(_previous);
            _previous = _current;
            _current = _ready;
            _ready = null;
        }
        
        return true;
    }
    
    private void Recycle(FrameGraph graph)
    {
        graph.Clear();
        _free.Push(graph);
    }
    
    public int BeginPresent()
    {
        if (!_active) return 1;
        
        _source = VideoRate.Rate;
        
        var frames = _clock.Advance(_source, Interp.EffectiveTarget);
        
        _budgetMs = _source > 0 ? 1000.0 / _source : 0.0;
        
        var target = Interp.EffectiveTarget;
        PaceMs = frames > 1 && target > 0 ? 1000.0 / target : 0.0;
        _frameStartMs = _watch.Elapsed.TotalMilliseconds;
        _rendered = 0;
        _renderedMs = 0.0;
        
        _interpolating = frames > 1 && _current.Interpolatable && _previous.Interpolatable && !_current.IsEmpty && _previous.Tris.Count > 0;
        
        if (!_interpolating) return 1;
        
        _matcher.Match(_current, _previous, MaxDelta);
        _map.Build(_current, _previous);
        _map.Resolve(_current);
        _frames = frames;
        return frames;
    }
    
    public void Compose(int index)
    {
        if (!_active) return;
        
        var weight = _interpolating && index < _frames ? _clock.Weights(_frames)[index] : 1f;
        var start = _watch.Elapsed.TotalMilliseconds;
        
        Replay(_current, _interpolating ? _previous : null, weight);
        
        _renderedMs += _watch.Elapsed.TotalMilliseconds - start;
        _rendered++;
    }
    
    /// <summary>
    /// an extra copy is only worth drawing if it still fits the frame the game is
    /// paying for, otherwise it is stealing the time the game needs to make the next one
    /// </summary>
    public bool Affordable(int index)
    {
        if (!_active || !_interpolating || index == 0 || _budgetMs <= 0.0) return true;
        if (_rendered == 0) return true;
        
        var elapsed = _watch.Elapsed.TotalMilliseconds - _frameStartMs;
        return elapsed + _renderedMs / _rendered <= _budgetMs;
    }
    
    public void EndPresent()
    {
        Begin();
    }
    
    private void Begin()
    {
        var wanted = _inner.Ready;
        if (wanted == _active) return;
        
        _active = wanted;
        _clock.Reset();
        _recording.Clear();
        _current.Clear();
        _previous.Clear();
    }
    
    private void Settle()
    {
        if (!_active || _current.IsEmpty) return;
        
        Replay(_current, null, 1f);
        _current.Clear();
        _current.Interpolatable = false;
    }
    
    private void Replay(FrameGraph graph, FrameGraph? previous, float weight)
    {
        var pixels = CollectionsMarshal.AsSpan(graph.Pixels);
        var ops = CollectionsMarshal.AsSpan(graph.Ops);
        var slots = CollectionsMarshal.AsSpan(graph.Slots);
        
        for (var i = 0; i < ops.Length; i++)
        {
            var slot = slots[i];
            
            switch (ops[i])
            {
                case GraphOp.DrawEnv:
                    _inner.SetDrawEnv(graph.Envs[slot]);
                    break;
                
                case GraphOp.Tri:
                    ReplayTri(graph, previous, slot, weight);
                    break;
                case GraphOp.Rect:
                {
                    var rect = graph.Rects[slot];
                    _inner.DrawRect(in rect.Rect, in rect.Flags);
                    break;
                }
                case GraphOp.Line:
                {
                    var line = graph.Lines[slot];
                    _inner.DrawLine(in line.A, in line.B, in line.Flags);
                    break;
                }
                case GraphOp.Fill:
                {
                    var fill = graph.Fills[slot];
                    _inner.FillRect(fill.X, fill.Y, fill.W, fill.H, fill.Color);
                    break;
                }
                case GraphOp.CopyVram:
                {
                    var copy = graph.Copies[slot];
                    _inner.CopyVram(copy.Sx, copy.Sy, copy.Dx, copy.Dy, copy.W, copy.H);
                    break;
                }
                case GraphOp.WriteVram:
                {
                    var write = graph.Writes[slot];
                    _inner.WriteVram(write.X, write.Y, write.W, write.H, pixels.Slice(write.Offset, write.Length));
                    break;
                }
            }
        }
    }
    
    private void ReplayTri(FrameGraph graph, FrameGraph? previous, int slot, float weight)
    {
        var tri = graph.Tris[slot];
        
        if (previous == null || weight >= 1f)
        {
            Emit(in tri, in tri.A, in tri.B, in tri.C);
            return;
        }
        
        var motion = graph.Motion[slot];
        
        Emit(in tri,
            (motion.Moved & 1) != 0 ? VertexMap.Blend(in tri.A, motion.AX, motion.AY, motion.AZ, weight) : tri.A,
            (motion.Moved & 2) != 0 ? VertexMap.Blend(in tri.B, motion.BX, motion.BY, motion.BZ, weight) : tri.B,
            (motion.Moved & 4) != 0 ? VertexMap.Blend(in tri.C, motion.CX, motion.CY, motion.CZ, weight) : tri.C);
    }
    
    private void Emit(in TriRecord tri, in HleVertex a, in HleVertex b, in HleVertex c)
    {
        var va = Attach(in a, tri.OffsetX, tri.OffsetY);
        var vb = Attach(in b, tri.OffsetX, tri.OffsetY);
        var vc = Attach(in c, tri.OffsetX, tri.OffsetY);
        
        _inner.DrawTri(in va, in vb, in vc, in tri.Flags); //send interpolated tri to backend
    }
    
    private static HleVertex Detach(in HleVertex v, float offsetX, float offsetY)
    {
        var d = v;
        d.X -= offsetX;
        d.Y -= offsetY;
        return d;
    }
    
    private static HleVertex Attach(in HleVertex v, float offsetX, float offsetY)
    {
        var a = v;
        a.X += offsetX;
        a.Y += offsetY;
        return a;
    }
    
    private static bool HasDepth(in TriRecord from, in TriRecord to)
    {
        return from.A.HasGteZ && from.B.HasGteZ && from.C.HasGteZ && to.A.HasGteZ && to.B.HasGteZ && to.C.HasGteZ;
    }
}
