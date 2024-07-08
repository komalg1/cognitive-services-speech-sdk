// <copyright file="Startup.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE.md file in the project root for full license information.
// </copyright>

using Microsoft.Azure.Functions.Extensions.DependencyInjection;

[assembly: FunctionsStartup(typeof(FetchTranscription.Startup))]

namespace FetchTranscription
{
    using System;

    using Azure.Messaging.ServiceBus;
    using Azure.Storage;
    using Azure.Storage.Blobs;

    using Connector;
    using Connector.Database;

    using Microsoft.Azure.Functions.Extensions.DependencyInjection;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;

    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            _ = builder ?? throw new ArgumentNullException(nameof(builder));

            if (FetchTranscriptionEnvironmentVariables.UseSqlDatabase)
            {
                builder.Services.AddDbContext<IngestionClientDbContext>(
                  options => SqlServerDbContextOptionsExtensions.UseSqlServer(options, FetchTranscriptionEnvironmentVariables.DatabaseConnectionString));
            }

            var startServiceBusConnectionString = FetchTranscriptionEnvironmentVariables.StartTranscriptionServiceBusConnectionString;
            var fetchServiceBusConnectionString = FetchTranscriptionEnvironmentVariables.FetchTranscriptionServiceBusConnectionString;
            var completedServiceBusConnectionString = FetchTranscriptionEnvironmentVariables.CompletedServiceBusConnectionString;
            var storageConnectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            var blobServiceClient = new BlobServiceClient(storageConnectionString);
            var storageCredential = new StorageSharedKeyCredential(
                GetValueFromConnectionString("AccountName", storageConnectionString),
                GetValueFromConnectionString("AccountKey", storageConnectionString));

            builder.Services.AddSingleton(blobServiceClient);
            builder.Services.AddSingleton(storageCredential);

            builder.Services.AddTransient<IStorageConnector, StorageConnector>();
            builder.Services.AddSingleton(provider =>
            {
                var client = provider.GetRequiredService<ServiceBusClient>();
                var connectionString = FetchTranscriptionEnvironmentVariables.StartTranscriptionServiceBusConnectionString;
                var entityPath = ServiceBusConnectionStringProperties.Parse(connectionString).EntityPath;
                return client.CreateSender(entityPath);
            });

            builder.Services.AddSingleton(provider =>
            {
                var client = provider.GetRequiredService<ServiceBusClient>();
                var connectionString = FetchTranscriptionEnvironmentVariables.FetchTranscriptionServiceBusConnectionString;
                Console.WriteLine($"EntityPath: {ServiceBusConnectionStringProperties.Parse(connectionString).EntityPath}");
                var entityPath = ServiceBusConnectionStringProperties.Parse(connectionString).EntityPath;

                // ServiceBusConnectionStringProperties.Parse(FetchTranscriptionEnvironmentVariables.StartTranscriptionServiceBusConnectionString).EntityPath
                return client.CreateSender(entityPath);
            });

            if (!string.IsNullOrEmpty(FetchTranscriptionEnvironmentVariables.CompletedServiceBusConnectionString))
            {
                // builder.Services.AddSingleton(new ServiceBusClient(FetchTranscriptionEnvironmentVariables.CompletedServiceBusConnectionString));
                builder.Services.AddSingleton(provider =>
                {
                    var client = provider.GetRequiredService<ServiceBusClient>();
                    var connectionString = FetchTranscriptionEnvironmentVariables.CompletedServiceBusConnectionString;
                    var entityPath = ServiceBusConnectionStringProperties.Parse(connectionString).EntityPath;
                    return client.CreateSender(entityPath);
                });
            }
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
