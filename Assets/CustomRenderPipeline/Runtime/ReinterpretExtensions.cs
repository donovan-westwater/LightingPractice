using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public static class ReinterpretExtensions
{
    //We make sure that the intValue shares the same space as the float
    //This allows us to reinterpet the int as a float
    //We can't do this in C# via casting as floats and ints are strongly typed
    [StructLayout(LayoutKind.Explicit)]
    struct IntFloat
    {
        [FieldOffset(0)]
        public int intValue;
        [FieldOffset(0)]
        public float floatValue;
    }
    //We need a function to convert the ints into floats
    //if we let the GPU do it it will get garbled
    public static float ReinterpretAsFloat(this int value)
    {
        IntFloat converter = default;
        converter.intValue = value;
        return converter.floatValue;
    }
}
