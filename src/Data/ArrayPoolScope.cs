using System;
using System.Buffers;

namespace BinGet.Data;

public readonly struct ArrayPoolScope<T> : IDisposable {
    public readonly T[] Buffer;

    public readonly Memory<T> AsMemory() {
        return Buffer.AsMemory();
    }
    
    public ArrayPoolScope(int size) {
        Buffer = ArrayPool<T>.Shared.Rent(size);
    }

    public void Dispose() {
        ArrayPool<T>.Shared.Return(Buffer);
    }
}