// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Diagnostics.Tracing;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.TestObjects
{
    public class TestEventSourceNoAttributes : EventSource
    {
        public static readonly TestEventSourceNoAttributes Logger = new TestEventSourceNoAttributes();

        public void NoArgEvent1()
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(1);
            }
        }

        public void IntArgEvent2(int arg)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(2, arg);
            }
        }

        public void LongArgEvent3(long arg)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(3, arg);
            }
        }

        [Event(4, Message = "Check if it is logged")]
        public void ObjectArrayEvent4(int arg0, string arg1, int arg2, string arg3, int arg4)
        {
            if (this.IsEnabled())
            {
                object[] args = new object[5];
                args[0] = arg0;
                args[1] = (object)arg1;
                args[2] = arg2;
                args[3] = arg3;
                args[4] = arg4;
                this.WriteEvent(4, args);
            }
        }

        public void StringArgEvent5(string arg)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(5, arg);
            }
        }

        public void TwoIntArgEvent6(int arg1, int arg2)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(6, arg1, arg2);
            }
        }

        public void TwoLongArgEvent7(long arg1, long arg2)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(7, arg1, arg2);
            }
        }

        public void StringAndIntArgEvent8(string arg1, int arg2)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(8, arg1, arg2);
            }
        }

        public void StringAndLongArgEvent9(string arg1, long arg2)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(9, arg1, arg2);
            }
        }

        public void StringAndStringArgEvent10(string arg1, string arg2)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(10, arg1, arg2);
            }
        }

        public void ThreeIntArgEvent11(int arg1, int arg2, int arg3)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(11, arg1, arg2, arg3);
            }
        }

        public void ThreeLongArgEvent12(long arg1, long arg2, long arg3)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(12, arg1, arg2, arg3);
            }
        }

        public void StringAndTwoIntArgEvent13(string arg1, int arg2, int arg3)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(13, arg1, arg2, arg3);
            }
        }

        public void ThreeStringArgEvent14(string arg1, string arg2, string arg3)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(14, arg1, arg2, arg3);
            }
        }

        public void SendEnumsEvent15(MyColor color, MyFlags flags)
        {
            this.WriteEvent(15, (int)color, (int)flags);
        }
    }

    public enum MyColor
    {
        Red,
        Blue,
        Green,
    }

    [Flags]
    public enum MyFlags
    {
        Flag1 = 1,
        Flag2 = 2,
        Flag3 = 4,
    }
}
