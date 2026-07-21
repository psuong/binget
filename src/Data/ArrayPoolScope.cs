using System;
using System.Buffers;

namespace BinGet.Data;

/// <summary>
/// Wraps ArrayPool{T} in an easy to use <see cref="IDisposable"/> scope.
/// </summary>
/// <typeparam name="T">Any type that needs to be pooled.</typeparam>
public readonly struct ArrayPoolScope<T> : IDisposable {
    public readonly T[] Buffer;

    /// <summary>
    /// Returns the array as a continuous block of memory.
    /// </summary>
    /// <returns><see cref="Memory{T}"/></returns>
    public readonly Memory<T> AsMemory() {
        return Buffer.AsMemory();
    }
    
    /// <summary>
    /// Primary constructor to request an <see cref="Array"/> of <typeparamref name="T"/>.
    /// </summary>
    /// <param name="size">The total number of elements in the <see cref="Array"/>.</param>
    public ArrayPoolScope(int size) {
        Buffer = ArrayPool<T>.Shared.Rent(size);
    }

    /// <summary>
    /// Returns the <see cref="Array"/> back to the <see cref="ArrayPool{T}"/>.
    /// </summary>
    public void Dispose() {
        ArrayPool<T>.Shared.Return(Buffer);
    }
}