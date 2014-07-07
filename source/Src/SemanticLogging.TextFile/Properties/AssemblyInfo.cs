// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;

[assembly: AssemblyTitle("Enterprise Library Semantic Logging Application Block")]
[assembly: AssemblyDescription("Enterprise Library Semantic Logging Application Block")]

[assembly: SecurityTransparent]

[assembly: AssemblyVersion("2.0.0.0")]
[assembly: AssemblyFileVersion("2.0.1406.1")]
[assembly: AssemblyInformationalVersion("2.0.1406.1")]

[assembly: ComVisible(false)]

#if SIGN
[assembly: InternalsVisibleTo("Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw, PublicKey=0024000004800000940000000602000000240000525341310004000001000100b5fc90e7027f67871e773a8fde8938c81dd402ba65b9201d60593e96c492651e889cc13f1415ebb53fac1131ae0bd333c5ee6021672d9718ea31a8aebd0da0072f25d87dba6fc90ffd598ed4da35e44c398c454307e8e33b8426143daec9f596836f97c8f74750e5975c64e2189f45def46b2a2b1247adc3652bf5c308055da9")]
[assembly: InternalsVisibleTo("Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests, PublicKey=0024000004800000940000000602000000240000525341310004000001000100b5fc90e7027f67871e773a8fde8938c81dd402ba65b9201d60593e96c492651e889cc13f1415ebb53fac1131ae0bd333c5ee6021672d9718ea31a8aebd0da0072f25d87dba6fc90ffd598ed4da35e44c398c454307e8e33b8426143daec9f596836f97c8f74750e5975c64e2189f45def46b2a2b1247adc3652bf5c308055da9")]
#else
[assembly: InternalsVisibleTo("Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw")]
[assembly: InternalsVisibleTo("Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests")]
#endif