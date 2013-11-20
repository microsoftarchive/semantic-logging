// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Diagnostics.Tracing;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.TestObjects
{
    [EventSource]
    internal class MultipleTypesEventSource : EventSource
    {
        public enum Color
        {
            Red,
            Blue,
            Gree
        }

        internal static readonly MultipleTypesEventSource Log = new MultipleTypesEventSource();

        [Event(1)]
        internal void ManyTypes(byte arg0, int arg1, uint arg11, long arg2, ulong arg22, double arg3, short arg5, ushort arg6, SByte arg7, bool arg8, string arg9,
            Guid arg14, Color arg16, Single arg17)
        {
            WriteEvent(1, arg0, arg1, arg11, arg2, arg22, arg3, arg5, arg6, arg7, arg8, arg9,
                arg14, arg16, arg17);
        }
    }
}
