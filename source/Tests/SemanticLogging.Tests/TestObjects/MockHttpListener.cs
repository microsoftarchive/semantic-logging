using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.TestObjects
{
    public class MockHttpListener
    {
        private readonly HttpListener listener = new HttpListener();

        private readonly int portTryRange;

        private int port;

        private Task contextTask;

        public MockHttpListener(int port = 3620, int portTryRange = 100)
        {
            this.port = port;

            this.portTryRange = portTryRange;
        }

        public void Stop()
        {
            if (this.listener != null)
            {
                this.listener.Close();
            }
        }

        public string Start(MockHttpListenerResponse message)
        {
            string endpoint = null;

            for (int i = 0; i < this.portTryRange; i++)
            {
                try
                {
                    var ep = string.Format("http://localhost:{0}/", port);
                    this.listener.Prefixes.Clear();
                    this.listener.Prefixes.Add(ep);
                    this.listener.Start();

                    endpoint = ep;
                    break;
                }
                catch (HttpListenerException)
                {
                    Debug.WriteLine("Failed to listen on port: {0}", port);
                    port++;
                }
            }

            if (endpoint == null)
            {
                throw new ApplicationException("Cannot open a port on localhost to listen on.");
            }

            this.contextTask = Task.Run(
                async () =>
                {
                    for (;;)
                    {
                        var ctx = await this.listener.GetContextAsync();

                        // Stop() was called - exit
                        if (!listener.IsListening)
                        {
                            break;
                        }

                        ctx.Response.StatusCode = message.ResponseCode;
                        ctx.Response.ContentType = message.ContentType;

                        if (message.Headers != null)
                        {
                            foreach (var header in message.Headers)
                            {
                                ctx.Response.Headers.Add(header);
                            }
                        }

                        byte[] buffer = Encoding.UTF8.GetBytes(message.Content);
                        ctx.Response.ContentLength64 = buffer.Length;
                        await ctx.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                        ctx.Response.Close();
                    }
                });

            return endpoint;
        }
    }
}
