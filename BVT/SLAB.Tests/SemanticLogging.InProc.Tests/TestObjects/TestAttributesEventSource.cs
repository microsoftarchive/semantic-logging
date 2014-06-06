// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System.Diagnostics.Tracing;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.InProc.Tests.TestObjects
{
    public sealed class TestAttributesEventSource : EventSource
    {
        public static readonly TestAttributesEventSource Logger = new TestAttributesEventSource();

        [Event(103, Opcode = EventOpcode.Reply, Version = 0x02, Task = Tasks.DBQuery, Message = "arg1- {0},arg2- {1},arg3- {2}")]
        public void NonDefaultOpcodeNonDefaultVersionEvent(int arg1, int arg2, int arg3)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(103, arg1, arg2, arg3);
            }
        }
        [Event(104)]
        public void NoTaskSpecfied(int arg1, int arg2, int arg3)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(104, arg1, arg3, arg3);
            }
        }

        [Event(105)]
        public void NoTaskSpecfied2(int arg1, int arg2, int arg3)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(105, arg1, arg3, arg3);
            }
        }

        public class Keywords
        {
            public const EventKeywords Database = (EventKeywords)0x0001;
            public const EventKeywords UILayer = (EventKeywords)0x0002;
            public const EventKeywords BusinessLayer = (EventKeywords)0x0004;
        }

        public class Tasks
        {
            public const EventTask DBQuery = (EventTask)1;
            public const EventTask PageLoad = (EventTask)2;
            public const EventTask Transaction = (EventTask)3;
        }
    }
}
