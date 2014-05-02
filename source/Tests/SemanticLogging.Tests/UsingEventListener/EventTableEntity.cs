// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.EventListeners
{
    /// <summary>
    /// Represents a log entry in a Windows Azure Table.
    /// </summary>
    internal class TestCloudTableEntry : TableEntity
    {
        public int EventId { get; set; }
        public DateTime EventDate { get; set; }
        public long Keywords { get; set; }
        public Guid ProviderId { get; set; }
        public string ProviderName { get; set; }
        public string InstanceName { get; set; }
        public int Level { get; set; }
        public string Message { get; set; }
        public int Opcode { get; set; }
        public int Task { get; set; }
        public int Version { get; set; }
        public string Payload { get; set; }
        public Dictionary<string, object> DeserializedPayload { get; private set; }
        public Dictionary<string, EntityProperty> RawPayloadProperties { get; private set; }
        public int ProcessId { get; set; }
        public int ThreadId { get; set; }
        public Guid? ActivityId { get; set; }
        public Guid? RelatedActivityId { get; set; }

        public override void ReadEntity(IDictionary<string, EntityProperty> properties, OperationContext operationContext)
        {
            base.ReadEntity(properties, operationContext);
            if (this.Payload != null)
            {
                this.DeserializedPayload = JsonConvert.DeserializeObject<Dictionary<string, object>>(this.Payload);
            }
            else
            {
                this.DeserializedPayload = new Dictionary<string, object>();
            }

            RawPayloadProperties = properties.Where(x => x.Key.StartsWith("Payload_")).ToDictionary(x => x.Key, x => x.Value);
        }
    }
}
