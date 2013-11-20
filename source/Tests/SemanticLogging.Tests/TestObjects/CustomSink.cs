// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.TestObjects
{
    public class CustomSink : IObserver<EventEntry>
    {
        public CustomSink(string required, int? optional)
        {
            this.Required = required;
            this.Optional = optional;
        }

        public string Required { get; set; }

        public int? Optional { get; set; }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(EventEntry value)
        {
        }
    }
}
