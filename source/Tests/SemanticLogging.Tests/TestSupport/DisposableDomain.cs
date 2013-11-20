// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.TestSupport
{
    internal sealed class DisposableDomain : IDisposable
    {
        private AppDomain domain = AppDomain.CreateDomain(Guid.NewGuid().ToString(), AppDomain.CurrentDomain.Evidence, AppDomain.CurrentDomain.SetupInformation);

        public void DoCallBack(CrossAppDomainDelegate action)
        {
            this.domain.DoCallBack(action);
        }

        public void Dispose()
        {
            AppDomain.Unload(this.domain);
        }
    }
}
