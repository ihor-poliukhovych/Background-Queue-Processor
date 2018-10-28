using System;
using Microsoft.WindowsAzure.Storage.Queue;

namespace BackgroundQueueProcessor
{
    public class QueueHandler
    {
        [QueueHandler("background-queue-processor")]
        public void HandleMessage(CloudQueueMessage message)
        {
            Console.WriteLine($"Message handled: {message.AsString}");
        }
    }
}