// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Diagnostics.Tracing;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.TestObjects
{
    [EventSource]
    public class DifferentEnumsEventSource : EventSource
    {
        public static readonly DifferentEnumsEventSource Log = new DifferentEnumsEventSource();

        [Event(306)]
        public void UsingEnumArguments(MyLongEnum arg1, MyIntEnum arg2, MyShortEnum arg3)
        {
            if (IsEnabled()) { WriteEvent(306, arg1, arg2, arg3); }
        }

        [Event(307)]
        public void UsingAllEnumArguments(MyLongEnum arg1, MyIntEnum arg2, MyShortEnum arg3,
            MyByteEnum arg4, MySByteEnum arg5, MyUShortEnum arg6, MyUIntEnum arg7, MyULongEnum arg8)
        {
            if (IsEnabled()) { WriteEvent(307, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8); }
        }
    }

    public enum MyIntEnum { Value1, Value2, Value3 }

    public enum MyLongEnum : long { Value1, Value2, Value3 }

    public enum MyShortEnum : short { Value1, Value2, Value3 }

    public enum MyByteEnum : byte { Value1, Value2, Value3 }

    public enum MySByteEnum : sbyte { Value1, Value2, Value3 }

    public enum MyUShortEnum : ushort { Value1, Value2, Value3 }

    public enum MyUIntEnum : uint { Value1, Value2, Value3 }

    public enum MyULongEnum : uint { Value1, Value2, Value3 }
}
