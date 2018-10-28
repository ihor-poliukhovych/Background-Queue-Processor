using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BackgroundQueueProcessor
{
    class Program
    {
        static void Main(string[] args)
        {
            var host = new HostBuilder()
                .ConfigureLogging(x => x.AddConsole())
                .ConfigureAppConfiguration(x => x.AddEnvironmentVariables())
                .ConfigureServices(x=>x.AddHostedService<QueueWorker>())
                .Build();
            
            host.Run(); 
        }
    }
}