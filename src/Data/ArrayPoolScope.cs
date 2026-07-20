using System;
using System.Buffers;

namespace BinGet.Data;

public readonly struct ArrayPoolScope : IDisposable {
    public readonly byte[] Buffer;

    public readonly Memory<byte> AsMemory() {
        return Buffer.AsMemory();
    }

    public readonly ReadOnlyMemory<byte> AsReadonlyMemory() {
        return Buffer.AsMemory();
    }
    
    public ArrayPoolScope(int size) {
        Buffer = ArrayPool<byte>.Shared.Rent(size);
    }

    public void Dispose() {
        ArrayPool<byte>.Shared.Return(Buffer);
    }
}