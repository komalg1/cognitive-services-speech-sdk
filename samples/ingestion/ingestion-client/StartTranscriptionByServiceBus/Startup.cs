// <copyright file="Startup.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE.md file in the project root for full license information.
// </copyright>

using Microsoft.Azure.Functions.Extensions.DependencyInjection;

[assembly: FunctionsStartup(typeof(StartTranscriptionByServiceBus.Startup))]

namespace StartTranscriptionByServiceBus
{
    using System;

    using Azure.Messaging.ServiceBus;
    using Azure.Storage;
    using Azure.Storage.Blobs;

    using Connector;

    using Microsoft.Azure.Functions.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection;

    using StartTranscriptionByTimer;

    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            _ = builder ?? throw new ArgumentNullException(nameof(builder));

            // var startServiceBusConnectionString = StartTranscriptionEnvironmentVariables.StartTranscriptionServiceBusConnectionString;
            // var fetchServiceBusConnectionString = StartTranscriptionEnvironmentVariables.FetchTranscriptionServiceBusConnectionString;
            // var storageConnectionString = StartTranscriptionEnvironmentVariables.AzureWebJobsStorage;
            // var blobServiceClient = new BlobServiceClient(storageConnectionString);
            // var storageCredential = new StorageSharedKeyCredential(
            //     GetValueFromConnectionString("AccountName", storageConnectionString),
            //     GetValueFromConnectionString("AccountKey", storageConnectionString));

            // builder.Services.AddSingleton(blobServiceClient);
            // builder.Services.AddSingleton(storageCredential);

            // builder.Services.AddTransient<IStorageConnector, StorageConnector>();
            // builder.Services.AddSingleton<ServiceBusClient>(sp =>
            // {
            //     return new ServiceBusClient(startServiceBusConnectionString);
            // });
            // builder.Services.AddSingleton<ServiceBusClient>(sp =>
            // {
            //     return new ServiceBusClient(fetchServiceBusConnectionString);
            // });
            builder.Services.AddSingleton(sp =>
            {
                var configValues = sp.GetRequiredService<IConfig>();
                return new BlobServiceClient(configValues.AzureWebJobsStorage);
            });

            builder.Services.AddSingleton(sp =>
            {
                var configValues = sp.GetRequiredService<IConfig>();
                return new StorageSharedKeyCredential(
                    GetValueFromConnectionString("AccountName", configValues.AzureWebJobsStorage),
                    GetValueFromConnectionString("AccountKey", configValues.AzureWebJobsStorage));
            });

            builder.Services.AddSingleton<IConfig, AppConfig>();
            builder.Services.AddTransient<IStorageConnector, StorageConnector>();
            builder.Services.AddSingleton<ServiceBusClient>(sp =>
            {
                var configValues = sp.GetRequiredService<IConfig>();
                return new ServiceBusClient(configValues.StartTranscriptionServiceBusConnectionString);
            });
            builder.Services.AddSingleton<ServiceBusClient>(sp =>
            {
                var configValues = sp.GetRequiredService<IConfig>();
                return new ServiceBusClient(configValues.FetchTranscriptionServiceBusConnectionString);
            });
        }

        private static string GetValueFromConnectionString(string key, string connectionString)
        {
            var split = connectionString.Split(';');

            foreach (var subConnectionString in split)
            {
                if (subConnectionString.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                {
                    return subConnectionString.Substring(key.Length + 1);
                }
            }

            return string.Empty;
        }
    }
}