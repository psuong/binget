using System;
using System.Collections.Generic;

namespace BinGet.Pool;

public readonly struct ObjectPoolScope<T> : IDisposable where T : class {
    public readonly T value;
    public readonly Action<T> reset;

    public ObjectPoolScope(Func<T> initialization, Action<T> reset, out T value) {
        value = ObjectPool<T>.Rent(initialization);
        this.value = value;
    }

    public void Dispose() {
        reset?.Invoke(value);
        ObjectPool<T>.Return(value);
    }
}

public static class ObjectPool<T> where T : class {

    private static readonly Stack<T> Pool;

    static ObjectPool() {
        Pool = new Stack<T>(1);
    }

    public static T Rent(Func<T> initialization) {
        if (Pool.Count > 0) {
            return Pool.Pop();
        } else {
            return initialization.Invoke();
        }
    }

    public static void Return(T value) {
        Pool.Push(value);
    }
}
