namespace RecompOne.Runtime.Events;

public enum VramTransfer { Load, Store, Move }

/// <summary>fires on a vrram copy by: LoadImage, StoreImage or MoveImage</summary>
public sealed class VramTransferEvent : GameEvent
{
    public int X, Y, W, H;
    public VramTransfer Direction;
}
