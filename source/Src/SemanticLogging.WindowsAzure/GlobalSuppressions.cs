// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

// This file is used by Code Analysis to maintain SuppressMessage 
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given 
// a specific target and scoped to a namespace, type, member, etc.
//
// To add a suppression to this file, right-click the message in the 
// Code Analysis results, point to "Suppress Message", and click 
// "In Suppression File".
// You do not need to add suppressions to this file manually.

[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1020:AvoidNamespacesWithFewTypes", Scope = "namespace", Target = "Microsoft.Practices.EnterpriseLibrary.SemanticLogging", Justification = "As designed")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1020:AvoidNamespacesWithFewTypes", Scope = "namespace", Target = "Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks", Justification = "As designed")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1020:AvoidNamespacesWithFewTypes", Scope = "namespace", Target = "Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks.WindowsAzure", Justification = "As designed")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1020:AvoidNamespacesWithFewTypes", Scope = "namespace", Target = "Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility", Justification = "As designed")]

[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Scope = "member", Target = "Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility.CloudEventEntryExtensions.#InitializePayload(Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks.WindowsAzure.CloudEventEntry,System.Collections.Generic.IList`1<System.Object>,Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Schema.EventSchema)", Justification = "Exception is logged")]
