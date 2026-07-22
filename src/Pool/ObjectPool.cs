using System;
using System.Collections.Generic;

namespace BinGet.Pool;

/// <summary>
/// Utility to provide a closure to use a pooled entry in the <see cref="ObjectPool{T}"/>.
/// </summary>
/// <typeparam name="T">Any class.</typeparam>
public readonly struct ObjectPoolScope<T> : IDisposable where T : class {
    public readonly T value;
    private readonly Action<T> reset;

    /// <summary>
    /// Initializes the closure.
    /// </summary>
    /// <param name="initialization">A function that creates an object of <typeparamref name="T"/>.</param>
    /// <param name="reset">A function to reset the object's state before returning to the pool.</param>
    /// <param name="value">The fetched object.</param>
    public ObjectPoolScope(Func<T> initialization, Action<T> reset, out T value) {
        value = ObjectPool<T>.Rent(initialization);
        this.value = value;
        this.reset = reset;
    }

    /// <summary>
    /// Resets the object and returns it to the <see cref="ObjectPool{T}"/>.
    /// </summary>
    public void Dispose() {
        reset?.Invoke(value);
        ObjectPool<T>.Return(value);
    }
}

/// <summary>
/// A generic object pool to constantly reuse an object at different time frames.
/// </summary>
/// <typeparam name="T">Any managed class.</typeparam>
public static class ObjectPool<T> where T : class {

    private static readonly Stack<T> Pool;

    static ObjectPool() {
        Pool = new Stack<T>(1);
    }

    /// <summary>
    /// Rents the object from the pool.
    /// </summary>
    /// <param name="initialization">An initialization function in case there are no objects in the pool.</param>
    /// <returns>A class of <typeparamref name="T"/>.</returns>
    public static T Rent(Func<T> initialization) {
        if (Pool.Count > 0) {
            return Pool.Pop();
        } else {
            return initialization.Invoke();
        }
    }

    /// <summary>
    /// Returns the rented object from <see cref="Rent"/> back to the pool for reuse.
    /// </summary>
    /// <param name="value">The object to return.</param>
    public static void Return(T value) {
        Pool.Push(value);
    }
}
