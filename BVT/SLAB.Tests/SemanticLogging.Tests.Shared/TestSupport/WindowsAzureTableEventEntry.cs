// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Microsoft.WindowsAzure.Storage.Table;
using System;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Shared.TestSupport
{
    public class WindowsAzureTableEventEntry : TableEntity
    {
        private const string RowKeyFormat = "{0}_{1}_{2:X5}";

        /// <summary>
        /// Initializes a new instance of the <see cref="WindowsAzureTableEventEntry"/> class.
        /// </summary>
        public WindowsAzureTableEventEntry()
        {
        }

        /// <summary>
        /// Gets or sets the event identifier.
        /// </summary>
        public int EventId { get; set; }

        /// <summary>
        /// Gets or sets the event date.
        /// </summary>
        public DateTime EventDate { get; set; }

        /// <summary>
        /// Gets or sets the keywords for the event.
        /// </summary>
        public long Keywords { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier for the event source.
        /// </summary>
        public Guid EventSourceGuid { get; set; }

        /// <summary>
        /// Gets or sets the friendly name of the class that is derived from the event source.
        /// </summary>
        public string EventSourceName { get; set; }

        /// <summary>
        /// Gets or sets the instance name where the entries are generated from.
        /// </summary>
        public string InstanceName { get; set; }

        /// <summary>
        /// Gets or sets the level of the event.
        /// </summary>
        public string Level { get; set; }

        /// <summary>
        /// Gets or sets the message for the event.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Gets or sets the operation code for the event.
        /// </summary>
        public int Opcode { get; set; }

        /// <summary>
        /// Gets or sets the task for the event.
        /// </summary>
        public int Task { get; set; }

        /// <summary>
        /// Gets or sets the version of the event.
        /// </summary>
        public int Version { get; set; }

        /// <summary>
        /// Gets or sets the payload for the event.
        /// </summary>
        public string Payload { get; set; }
    }
}
