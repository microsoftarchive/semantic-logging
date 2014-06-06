// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Diagnostics.Tracing;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.TestObjects
{
    public sealed class MockEventSourceOutProcEnum : EventSource
    {
        public static readonly MockEventSourceOutProcEnum Logger = new MockEventSourceOutProcEnum();

        public static class Tasks
        {
            public const EventTask Opcode = (EventTask)1;
        }

        [Event(2)]
        public void SendEnumsEvent15(MyColor a, MyFlags b)
        {
            this.WriteEvent(2, (int)a, (int)b);
        }

        [Event(3, Task = Tasks.Opcode, Opcode = EventOpcode.Resume)]
        public void SendEnumsEvent16(MyColor a, MyFlags b)
        {
            this.WriteEvent(3, a, b);
        }

        [Event(4)]
        public void SaveExpenseStarted(Guid expenseId)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(4, expenseId);
            }
        }

        public enum MyColor { Red, Blue, Green }

        public enum MyFlags { Flag1 = 1, Flag2 = 2, Flag3 = 4 }
    }
}
