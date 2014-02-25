// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Diagnostics.Tracing;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.InProc.Tests.TestObjects
{
    public class MockEventSourceInProcEnum : EventSource
    {
        public static readonly MockEventSourceInProcEnum Logger = new MockEventSourceInProcEnum();

        [Event(1)]
        public void InformationalWithEnum(Message message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(1, message.ToString());
            }
        }

        [Event(2)]
        public void SendEnumsEvent15(MyColor a, MyFlags b)
        {
            this.WriteEvent(2, (int)a, (int)b);
        }

        [Event(3, Task = EventTask.None, Opcode = EventOpcode.Resume)]
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

        [Event(5, Task = EventTask.None, Opcode = EventOpcode.Resume)]
        public void SendEnumsEvent17(MyColor a, MyFlags b)
        {
            this.WriteEvent(5, (int)a, (int)b);
        }

        public enum Message { LightMessage = 1, FullMessage = 2 }

        public enum MyColor { Red, Blue, Green }

        public enum MyFlags { Flag1 = 1, Flag2 = 2, Flag3 = 4 }
    }
}
