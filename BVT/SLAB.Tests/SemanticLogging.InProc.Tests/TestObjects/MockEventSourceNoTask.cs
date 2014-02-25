// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Diagnostics.Tracing;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.InProc.Tests.TestObjects
{
    public class MockEventSourceNoTask : EventSource
    {
        [Event(1, Level = EventLevel.Informational, Message = "message param")]
        public void Informational(string message)
        {
            if (this.IsEnabled(EventLevel.Informational, EventKeywords.None))
            {
                this.WriteEvent(1, message); 
            }
        }

        [Event(2, Opcode = EventOpcode.Start)]
        public void NoTaskSpecfied1(int event3Arg0, int event3Arg1, int event3Arg2)
        {
            if (this.IsEnabled(EventLevel.Informational, EventKeywords.None))
            {
                this.WriteEvent(2, event3Arg0, event3Arg0, event3Arg2); 
            }
        }

        [Event(3, Opcode = EventOpcode.Start)]
        public void NoTaskSpecfied2(int event3Arg0, int event3Arg1, int event3Arg2)
        {
        }

        public void Test(int event3Arg0, int event3Arg1, int event3Arg2)
        {
        }

        public void NoTaskNoOpCode1(int event3Arg0, int event3Arg1, int event3Arg2)
        {
            if (this.IsEnabled(EventLevel.Informational, EventKeywords.None))
            {
                this.WriteEvent(5, event3Arg0, event3Arg0, event3Arg2); 
            }
        }

        [Event(6, Opcode = EventOpcode.Start)]
        public void DifferentTypes(string strArg, int longArg)
        {
            if (this.IsEnabled(EventLevel.Informational, EventKeywords.None))
            {
                this.WriteEvent(6, strArg, longArg); 
            }
        }

        [Event(7, Opcode = EventOpcode.Start)]
        public void DifferentTypesInverted(string strArg, long longArg, int intArg)
        {
            if (this.IsEnabled(EventLevel.Informational, EventKeywords.None))
            {
                this.WriteEvent(7, intArg, strArg, longArg); 
            }
        }

        [Event(8, Opcode = EventOpcode.Start)]
        public void AllSupportedTypes(short srtArg, int intArg, long lngArg, float fltArg, TestEnum enumArg, Guid guidArg)
        {
            if (this.IsEnabled(EventLevel.Informational, EventKeywords.None))
            {
                this.WriteEvent(8, srtArg, intArg, lngArg, fltArg, (int)enumArg, guidArg); 
            }
        }

        [Event(65535, Opcode = EventOpcode.Start)]
        public void MaxValues(string strArg, long longArg, int intArg)
        {
            if (this.IsEnabled(EventLevel.Informational, EventKeywords.None))
            {
                this.WriteEvent(65535, intArg, strArg, longArg); 
            }
        }

        [Event(9, Level = EventLevel.Informational)]
        public void InformationalNoMessage(string message)
        {
            if (this.IsEnabled(EventLevel.Informational, EventKeywords.None))
            {
                this.WriteEvent(9, message); 
            }
        }

        [Event(10, Level = EventLevel.Informational, Message = "**{0}**")]
        public void InformationalMessageFormat(string message)
        {
            if (this.IsEnabled(EventLevel.Informational, EventKeywords.None))
            {
                this.WriteEvent(10, message); 
            }
        }

        public static readonly MockEventSourceNoTask Logger = new MockEventSourceNoTask();
    }
}
