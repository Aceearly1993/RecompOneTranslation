using RecompOne.Runtime.Hle;

namespace RecompOne.Runtime.Interp;

internal enum GraphOp : byte
{
    DrawEnv,
    Tri,
    Rect,
    Line,
    Fill,
    CopyVram,
    WriteVram
}

internal struct TriRecord
{
    public HleVertex A;
    public HleVertex B;
    public HleVertex C;
    public PrimFlags Flags;
    public ulong Key;
    public int Match;
    public float OffsetX;
    public float OffsetY;
}

internal struct TriMotion
{
    public float AX, AY, AZ;
    public float BX, BY, BZ;
    public float CX, CY, CZ;
    public byte Moved;
}

internal struct RectRecord
{
    public HleRect Rect;
    public PrimFlags Flags;
}

internal struct LineRecord
{
    public HleVertex A;
    public HleVertex B;
    public PrimFlags Flags;
}

internal struct FillRecord
{
    public int X;
    public int Y;
    public int W;
    public int H;
    public ushort Color;
}

internal struct CopyRecord
{
    public int Sx;
    public int Sy;
    public int Dx;
    public int Dy;
    public int W;
    public int H;
}

internal struct WriteRecord
{
    public int X;
    public int Y;
    public int W;
    public int H;
    public int Offset;
    public int Length;
}

internal sealed class FrameGraph
{
    public readonly List<GraphOp> Ops = [];
    public readonly List<int> Slots = [];
    public readonly List<HleDrawEnv> Envs = [];
    public readonly List<TriRecord> Tris = [];
    public readonly List<TriMotion> Motion = [];
    public readonly List<RectRecord> Rects = [];
    public readonly List<LineRecord> Lines = [];
    public readonly List<FillRecord> Fills = [];
    public readonly List<CopyRecord> Copies = [];
    public readonly List<WriteRecord> Writes = [];
    public readonly List<ushort> Pixels = [];
    
    public bool Interpolatable = true;
    
    public bool IsEmpty => Ops.Count == 0;
    
    public void Clear()
    {
        Ops.Clear();
        Slots.Clear();
        Envs.Clear();
        Tris.Clear();
        Motion.Clear();
        Rects.Clear();
        Lines.Clear();
        Fills.Clear();
        Copies.Clear();
        Writes.Clear();
        Pixels.Clear();
        Interpolatable = true;
    }
    
    public void Add(GraphOp op, int slot)
    {
        Ops.Add(op);
        Slots.Add(slot);
    }
    
    public int AddPixels(ReadOnlySpan<ushort> pixels)
    {
        var offset = Pixels.Count;
        foreach (var p in pixels) Pixels.Add(p);
        return offset;
    }
}
