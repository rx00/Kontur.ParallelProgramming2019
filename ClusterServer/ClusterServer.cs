﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using log4net;

namespace Cluster
{
    internal class ClusterServer
    {
        public ClusterServer(ServerOptions serverOptions, ILog log)
        {
            this.ServerOptions = serverOptions;
            this.log = log;
        }

        public void Start()
        {
            if (Interlocked.CompareExchange(ref isRunning, Running, NotRunning) == NotRunning)
            {
                httpListener = new HttpListener
                {
                    Prefixes =
                    {
                        $"http://+:{ServerOptions.Port}/{ServerOptions.MethodName}/"
                    }
                };

                Console.WriteLine($"Server is starting listening prefixes: {String.Join(";", httpListener.Prefixes)}");

                if (ServerOptions.Async)
                {
                    Console.WriteLine("Press ENTER to stop listening");
                    httpListener.StartProcessingRequestsAsync(CreateAsyncCallback(ServerOptions));
                }
                else
                    httpListener.StartProcessingRequestsSync(CreateSyncCallback(ServerOptions));
            }
        }

        public void Stop()
        {
            if (Interlocked.CompareExchange(ref isRunning, NotRunning, Running) == Running)
            {
                Console.WriteLine($"Stopping {ServerOptions.Port}");
                httpListener.Stop();
            }
        }

        public ServerOptions ServerOptions { get; }

        private Action<HttpListenerContext> CreateSyncCallback(ServerOptions parsedOptions)
        {
            return context =>
            {
                var currentRequestId = Interlocked.Increment(ref RequestsCount);
                log.InfoFormat("Thread #{0} received request #{1} at {2}",
                    Thread.CurrentThread.ManagedThreadId, currentRequestId, DateTime.Now.TimeOfDay);

                Thread.Sleep(parsedOptions.MethodDuration);

                var encryptedBytes = ClusterHelpers.GetBase64HashBytes(context.Request.QueryString["query"]);
                context.Response.OutputStream.Write(encryptedBytes, 0, encryptedBytes.Length);

                log.InfoFormat("Thread #{0} sent response #{1} at {2}",
                    Thread.CurrentThread.ManagedThreadId, currentRequestId,
                    DateTime.Now.TimeOfDay);
            };
        }

        private Func<HttpListenerContext, Task> CreateAsyncCallback(ServerOptions parsedOptions)
        {
            return async context =>
            {
                var currentRequestNum = Interlocked.Increment(ref RequestsCount);
                Console.WriteLine("Thread #{0} received request #{1} at {2}",
                    Thread.CurrentThread.ManagedThreadId, currentRequestNum, DateTime.Now.TimeOfDay);

                await Task.Delay(parsedOptions.MethodDuration);
                //				Thread.Sleep(parsedArguments.MethodDuration);

                var encryptedBytes = ClusterHelpers.GetBase64HashBytes(context.Request.QueryString["query"]);
                await context.Response.OutputStream.WriteAsync(encryptedBytes, 0, encryptedBytes.Length);

                Console.WriteLine("Thread #{0} sent response #{1} at {2}",
                    Thread.CurrentThread.ManagedThreadId, currentRequestNum,
                    DateTime.Now.TimeOfDay);
            };
        }

     


        private int RequestsCount;

        private int isRunning = NotRunning;

        private const int Running = 1;
        private const int NotRunning = 0;

        private readonly ILog log;
        private HttpListener httpListener;
    }
}
