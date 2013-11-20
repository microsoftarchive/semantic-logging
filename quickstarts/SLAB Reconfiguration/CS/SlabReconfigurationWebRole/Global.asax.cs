// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using SlabReconfigurationWebRole.Messaging;

namespace SlabReconfigurationWebRole
{
    // Note: For instructions on enabling IIS6 or IIS7 classic mode, 
    // visit http://go.microsoft.com/?LinkId=9394801

    public partial class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();

            WebApiConfig.Register(GlobalConfiguration.Configuration);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            this.InitializeDiagnostics();
        }

        private static readonly Lazy<IMessageSender> messageSender = new Lazy<IMessageSender>(() => new FakeMessageSender());

        public static IMessageSender MessageSender
        {
            get { return messageSender.Value; }
        }

        partial void InitializeDiagnostics();
    }
}