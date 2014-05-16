// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.TestSupport
{
    public static class EnumerableAssert
    {
        public static void AreEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual)
        {
            CollectionAssert.AreEqual(expected.ToList(), actual.ToList());
        }
    }
}
