using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Configuration;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Observable;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.TestObjects;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Shared.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.Sinks
{
    [TestClass]
    public class ElasticSearchSinkFixture
    {
        private readonly string elasticSearchUri = ConfigurationManager.AppSettings["ElasticSearchUri"];
        private string indexPrefix = "testindex";
        private string type = "testtype";

        [TestInitialize]
        public void Initialize()
        {
            try
            {
                ElasticSearchHelper.DeleteIndex(elasticSearchUri);
            }
            catch (Exception exp)
            {
                Assert.Inconclusive(String.Format("Error occured connecting to ES: Message{0}, StackTrace: {1}", exp.Message, exp.StackTrace));
            }
        }

        [TestMethod]
        public void WhenUsingSinkProgramatically()
        {
            var index = string.Format(CultureInfo.InvariantCulture, "{0}-{1:yyyy.MM.dd}", this.indexPrefix, DateTime.UtcNow);
            var logger = MockEventSourceOutProc.Logger;

            EventTextFormatter formatter = new EventTextFormatter();
            EventSourceSettings settings = new EventSourceSettings("MockEventSourceOutProc", null, EventLevel.LogAlways);
            var subject = new EventEntrySubject();
            subject.LogToElasticSearch("testInstance", elasticSearchUri, this.indexPrefix, this.type, /* flattenPayload: false, */ bufferingInterval: TimeSpan.FromSeconds(5));

            QueryResult result = null;
            SinkSettings sinkSettings = new SinkSettings("essink", subject, new List<EventSourceSettings>() { { settings } });
            List<SinkSettings> sinks = new List<SinkSettings>() { { sinkSettings } };
            TraceEventServiceConfiguration svcConfiguration = new TraceEventServiceConfiguration(sinks);
            using (TraceEventService collector = new TraceEventService(svcConfiguration))
            {
                collector.Start();
                try
                {
                    for (int n = 0; n < 10; n++)
                    {
                        logger.LogSomeMessage("message " + n.ToString());
                    }

                    result = ElasticSearchHelper.PollUntilEvents(elasticSearchUri, index, this.type, 10);
                }
                finally
                {
                    collector.Stop();
                }
            }

            Assert.AreEqual(10, result.Hits.Total);
            for (int n = 0; n < 10; n++)
            {
                Assert.IsNotNull(result.Hits.Hits.SingleOrDefault(h => h.Source["Payload_message"].ToString() == "message " + n.ToString()), "'message {0}' should be a hit", n);
            }
        }

        [TestMethod]
        public void WhenUsingSinkWithDefaultConfig()
        {
            var index = string.Format(CultureInfo.InvariantCulture, "{0}-{1:yyyy.MM.dd}", "logstash", DateTime.UtcNow);

            var logger = MockEventSourceOutProc.Logger;

            QueryResult result = null;
            var svcConfiguration = TraceEventServiceConfiguration.Load("Configurations\\ElasticSearchSink\\ElasticSinkMandatoryProperties.xml");
            using (TraceEventService collector = new TraceEventService(svcConfiguration))
            {
                collector.Start();
                try
                {
                    for (int n = 0; n < 10; n++)
                    {
                        logger.LogSomeMessage("some message" + n.ToString());
                    }

                    Thread.Sleep(TimeSpan.FromSeconds(45));
                    result = ElasticSearchHelper.GetEvents(elasticSearchUri, index, "etw");
                }
                finally
                {
                    collector.Stop();
                }
            }

            Assert.AreEqual(10, result.Hits.Total);
            StringAssert.Contains(result.Hits.Hits[0].Source["Payload_message"].ToString(), "some message");
        }

        [TestMethod]
        public void WhenUsingSinkWithNonDefaultConfig()
        {
        }
    }
}
