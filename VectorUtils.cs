using Godot;
using System;
using System.Globalization;

public static class VectorUtils {
    public static Vector3 ToVector3(this Vector2 vector) {
        return new Vector3(vector.X, vector.Y, 0);
    }
    public static Vector2 ToVector2(this Vector3 vector) {
        return new Vector2(vector.X, vector.Y);
    }
}

public struct Vector2Array {
    // Flatten vector2s into a concatenated array of X and Y values so that we can pass them into the compute shader as a single float array
    public float[] _internalFloatArray;
    readonly int _arraySize;

    public int Length {
        get { return _arraySize; }
    }
    public int InternalArraySize {
        get { return _arraySize * 2; }
    }

    public Vector2Array(int arraySize) {
        _internalFloatArray = new float[arraySize * 2];
        _arraySize = arraySize;
    }

    public Vector2 this[int index] {
        get {
            return new Vector2(_internalFloatArray[index], _internalFloatArray[index + _arraySize]);
        }
        set {
            _internalFloatArray[index] = value.X;
            _internalFloatArray[index + _arraySize] = value.Y;
        }
    }

    /* Add all vectors of the specified array to this array. */
    public void AddArray(Vector2Array array) {
        if (this.InternalArraySize != array.InternalArraySize) { throw new IndexOutOfRangeException("Added arrays cannot be of different sizes."); }
        for (int i = 0; i < this.InternalArraySize; i++) {
            this._internalFloatArray[i] += array._internalFloatArray[i];
        }
    }
}
