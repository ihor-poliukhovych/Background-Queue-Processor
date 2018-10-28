using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;

namespace BackgroundQueueProcessor
{
    internal class QueueWorker : BackgroundService
    {
        private IConfiguration _config;
        private readonly ILogger<QueueWorker> _logger;
        private readonly IServiceProvider _serviceProvider;

        public QueueWorker(IConfiguration config, ILogger<QueueWorker> logger, IServiceProvider serviceProvider)
        {
            _config = config;
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var storageAccount = CloudStorageAccount.Parse(
                "DefaultEndpointsProtocol=https;AccountName=ipoli;AccountKey=pmW6WJjfOf8LIV635f/Mqic7N6jzNteHfSkNR3FMNc+T8TWWrxejBuFtDZwzmTwGirPsby2qwcdgiPkgB35iaw==;EndpointSuffix=core.windows.net");
            //_config["connectionStrings:background-queue-processor"]);

            //Create the queueClient
            var queueClient = storageAccount.CreateCloudQueueClient();
            BindQueueMethods(queueClient);

            if (!int.TryParse(_config["LongDelay"], out int longDelay))
            {
                longDelay = 1000;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                foreach (var binding in _queueBindings)
                {
                    var message = await binding.Queue.GetMessageAsync();

                    //If there no massages then sleep for a bit and await.
                    //ne common pattern here is to do exponential sleep time
                    //up to max. If overnight there no work then you want
                    //to sleep a much as possible
                    if (message == null)
                    {
                        await Task.Delay(longDelay);
                        continue;
                    }

                    if (message.DequeueCount > 3)
                    {
                        //TODO: here you would transfer your message to table/blob storage
                        //for latest recovery and analyzing. Kind of a manual dead letter
                        //This would handle bad message
                        _logger.LogCritical($"Giving up processing {message.Id}: {message}");
                        await binding.Queue.DeleteMessageAsync(message);
                        continue;
                    }

                    try
                    {
                        _logger.LogInformation($"Processing data {message.AsString}");

                        using (var scope = _serviceProvider.CreateScope())
                        {
                            var handler = ActivatorUtilities.CreateInstance(scope.ServiceProvider,
                                binding.Method.DeclaringType);

                            binding.Method.Invoke(handler, new object[] {message});
                        }

                        await binding.Queue.DeleteMessageAsync(message);
                        _logger.LogInformation($"Processing data complete");

                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, $"Unknown error processing message {message.Id}");
                        throw;
                    }
                }
            }
        }

        private List<QueueBinding> _queueBindings = new List<QueueBinding>();

        private void BindQueueMethods(CloudQueueClient queueClient)
        {
            foreach (var type in Assembly.GetEntryAssembly().GetTypes())
            {
                foreach (var method in type.GetMethods())
                {
                    var attr = method.GetCustomAttribute<QueueHandlerAttribute>();
                    if (attr != null)
                    {
                        _queueBindings.Add(new QueueBinding
                        {
                            QueueName = attr.QueueName,
                            Method = method,
                            Queue = queueClient.GetQueueReference(attr.QueueName)
                        });
                    }
                }
            }
        }

        class QueueBinding
        {
            public string QueueName { get; set; }
            public MethodInfo Method { get; set; }
            public CloudQueue Queue { get; set; }
        }
    }
}