// <copyright file="IConfig.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE.md file in the project root for full license information.
// </copyright>

namespace StartTranscriptionByTimer
{
    public interface IConfig
    {
        bool AddDiarization { get; }

        bool AddWordLevelTimestamps { get; }

        bool IsAzureGovDeployment { get; }

        bool IsByosEnabledSubscription { get; }

        int MessagesPerFunctionExecution { get; }

        int FilesPerTranscriptionJob { get; }

        int RetryLimit { get; }

        int InitialPollingDelayInMinutes { get; }

        int MaxPollingDelayInMinutes { get; }

        string AudioInputContainer { get; }

        string AzureServiceBus { get; }

        string AzureSpeechServicesKey { get; }

        string AzureSpeechServicesEndpointUri { get; }

        string AzureWebJobsStorage { get; }

        string CustomModelId { get; }

        string ErrorFilesOutputContainer { get; }

        string ErrorReportOutputContainer { get; }

        string FetchTranscriptionServiceBusConnectionString { get; }

        string Locale { get; }

        string ProfanityFilterMode { get; }

        string PunctuationMode { get; }

        string StartTranscriptionServiceBusConnectionString { get; }

        string StartTranscriptionFunctionTimeInterval { get; }
    }
}
