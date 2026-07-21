using System;
using System.Buffers;

namespace BinGet.Data;

/// <summary>
/// Wraps ArrayPool{T} in an easy to use <see cref="IDisposable"/> scope.
/// </summary>
/// <typeparam name="T">Any type that needs to be pooled.</typeparam>
/// <remarks>
/// Primary constructor to request an <see cref="Array"/> of <typeparamref name="T"/>.
/// </remarks>
/// <param name="size">The total number of elements in the <see cref="Array"/>.</param>
public readonly struct ArrayPoolScope<T>(int size) : IDisposable {
    private readonly T[] buffer = ArrayPool<T>.Shared.Rent(size);

    /// <summary>
    /// Returns the array as a continuous block of memory.
    /// </summary>
    /// <returns><see cref="Memory{T}"/></returns>
    public readonly Memory<T> AsMemory() {
        return buffer.AsMemory();
    }

    /// <summary>
    /// Returns the <see cref="Array"/> back to the <see cref="ArrayPool{T}"/>.
    /// </summary>
    public void Dispose() {
        ArrayPool<T>.Shared.Return(buffer);
    }
}
