// <copyright file="StartTranscriptionByTimer.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE.md file in the project root for full license information.
// </copyright>

namespace StartTranscriptionByTimer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus;
    using Connector;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Extensions.Logging;

    public class StartTranscriptionByTimer
    {
        private const double MessageReceiveTimeoutInSeconds = 60;
        private readonly ServiceBusClient startServiceBusClient;

        private readonly ServiceBusClient fetchServiceBusClient;
        private readonly ServiceBusReceiverOptions serviceBusReceiverOptions;

        // private static readonly ServiceBusClient ServiceBusClient = new ServiceBusClient(StartTranscriptionEnvironmentVariables.StartTranscriptionServiceBusConnectionString);

        // private static readonly ServiceBusReceiverOptions ServiceBusReceiverOptions = new ServiceBusReceiverOptions() { PrefetchCount = StartTranscriptionEnvironmentVariables.MessagesPerFunctionExecution };

        // private static readonly ServiceBusReceiver ServiceBusReceiver = ServiceBusClient.CreateReceiver(ServiceBusConnectionStringProperties.Parse(StartTranscriptionEnvironmentVariables.StartTranscriptionServiceBusConnectionString).EntityPath, ServiceBusReceiverOptions);
        private readonly IStorageConnector storageConnector;

        private readonly IConfig config;

        public StartTranscriptionByTimer(IStorageConnector storageConnector, ServiceBusClient startServiceBusClient, ServiceBusClient fetchServiceBusClient, IConfig config)
        {
            this.storageConnector = storageConnector;
            this.startServiceBusClient = startServiceBusClient;
            this.fetchServiceBusClient = fetchServiceBusClient;
            this.config = config;
            this.serviceBusReceiverOptions = new ServiceBusReceiverOptions
            {
                PrefetchCount = this.config.MessagesPerFunctionExecution,
            };
        }

        [FunctionName("StartTranscriptionByTimer")]
        public async Task Run([TimerTrigger("%StartTranscriptionFunctionTimeInterval%")] TimerInfo timerInfo, ILogger log)
        {
            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            if (timerInfo == null)
            {
                throw new ArgumentNullException(nameof(timerInfo));
            }

            var startDateTime = DateTime.UtcNow;
            log.LogInformation($"C# Timer trigger function v3 executed at: {startDateTime}. Next occurrence on {timerInfo.Schedule.GetNextOccurrence(startDateTime)}.");

            var validServiceBusMessages = new List<ServiceBusReceivedMessage>();
            var transcriptionHelper = new StartTranscriptionHelper(log, this.storageConnector, this.startServiceBusClient, this.fetchServiceBusClient, this.config);

            log.LogInformation("Pulling messages from queue...");
            log.LogInformation($"StartTranscriptionServiceBusConnectionString dsfdfdfs: {this.config.StartTranscriptionServiceBusConnectionString}");
            var serviceBusReceiver = this.startServiceBusClient.CreateReceiver(
                ServiceBusConnectionStringProperties.Parse(this.config.StartTranscriptionServiceBusConnectionString).EntityPath, this.serviceBusReceiverOptions);
            var messages = await serviceBusReceiver.ReceiveMessagesAsync(this.config.MessagesPerFunctionExecution, TimeSpan.FromSeconds(MessageReceiveTimeoutInSeconds)).ConfigureAwait(false);

            if (messages == null || !messages.Any())
            {
                log.LogInformation($"Got no messages in this iteration.");
                return;
            }

            log.LogInformation($"Got {messages.Count} in this iteration.");
            foreach (var message in messages)
            {
                if (message.LockedUntil > DateTime.UtcNow.AddSeconds(5))
                {
                    try
                    {
                        if (transcriptionHelper.IsValidServiceBusMessage(message))
                        {
                            await serviceBusReceiver.RenewMessageLockAsync(message).ConfigureAwait(false);
                            validServiceBusMessages.Add(message);
                        }
                        else
                        {
                            await serviceBusReceiver.CompleteMessageAsync(message).ConfigureAwait(false);
                        }
                    }
                    catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessageLockLost)
                    {
                        log.LogInformation($"Message lock expired for message. Ignore message in this iteration.");
                    }
                }
            }

            if (!validServiceBusMessages.Any())
            {
                log.LogInformation("No valid messages were found in this function execution.");
                return;
            }

            log.LogInformation($"Pulled {validServiceBusMessages.Count} valid messages from queue.");

            await transcriptionHelper.StartTranscriptionsAsync(validServiceBusMessages, serviceBusReceiver, startDateTime).ConfigureAwait(false);
        }
    }
}
