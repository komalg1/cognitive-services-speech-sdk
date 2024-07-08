// <copyright file="AppConfig.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE.md file in the project root for full license information.
// </copyright>

namespace StartTranscriptionByTimer
{
    using System;

    using Connector;
    using Connector.Constants;

    public class AppConfig : IConfig
    {
        public bool AddDiarization => bool.TryParse(Environment.GetEnvironmentVariable(nameof(this.AddDiarization)), out var addDiarization) && addDiarization;

        public bool AddWordLevelTimestamps => bool.TryParse(Environment.GetEnvironmentVariable(nameof(this.AddWordLevelTimestamps)), out var addWordLevelTimestamps) && addWordLevelTimestamps;

        public bool IsAzureGovDeployment => bool.TryParse(Environment.GetEnvironmentVariable(nameof(this.IsAzureGovDeployment)), out var isAzureGovDeployment) && isAzureGovDeployment;

        public bool IsByosEnabledSubscription => bool.TryParse(Environment.GetEnvironmentVariable(nameof(this.IsByosEnabledSubscription)), out var isByosEnabledSubscription) && isByosEnabledSubscription;

        public int MessagesPerFunctionExecution => int.TryParse(Environment.GetEnvironmentVariable(nameof(this.MessagesPerFunctionExecution), EnvironmentVariableTarget.Process), out var messagesPerFunctionExecution) ? messagesPerFunctionExecution.ClampInt(1, Constants.MaxMessagesPerFunctionExecution) : Constants.DefaultMessagesPerFunctionExecution;

        public int FilesPerTranscriptionJob => int.TryParse(Environment.GetEnvironmentVariable(nameof(this.FilesPerTranscriptionJob), EnvironmentVariableTarget.Process), out var filesPerTranscriptionJob) ? filesPerTranscriptionJob.ClampInt(1, Constants.MaxFilesPerTranscriptionJob) : Constants.DefaultFilesPerTranscriptionJob;

        public int RetryLimit => int.TryParse(Environment.GetEnvironmentVariable(nameof(this.RetryLimit), EnvironmentVariableTarget.Process), out var retryLimit) ? retryLimit.ClampInt(1, Constants.MaxRetryLimit) : Constants.DefaultRetryLimit;

        public int InitialPollingDelayInMinutes => int.TryParse(Environment.GetEnvironmentVariable(nameof(this.InitialPollingDelayInMinutes), EnvironmentVariableTarget.Process), out var initialPollingDelayInMinutes) ? initialPollingDelayInMinutes.ClampInt(2, Constants.MaxInitialPollingDelayInMinutes) : Constants.DefaultInitialPollingDelayInMinutes;

        public int MaxPollingDelayInMinutes => int.TryParse(Environment.GetEnvironmentVariable(nameof(this.MaxPollingDelayInMinutes), EnvironmentVariableTarget.Process), out var maxPollingDelayInMinutes) ? maxPollingDelayInMinutes : Constants.DefaultMaxPollingDelayInMinutes;

        public string AudioInputContainer => Environment.GetEnvironmentVariable(nameof(this.AudioInputContainer), EnvironmentVariableTarget.Process);

        public string AzureServiceBus => Environment.GetEnvironmentVariable(nameof(this.AzureServiceBus), EnvironmentVariableTarget.Process);

        public string AzureSpeechServicesKey => Environment.GetEnvironmentVariable(nameof(this.AzureSpeechServicesKey), EnvironmentVariableTarget.Process);

        public string AzureSpeechServicesEndpointUri => Environment.GetEnvironmentVariable(nameof(this.AzureSpeechServicesEndpointUri), EnvironmentVariableTarget.Process).TrimEnd('/') + '/';

        public string AzureWebJobsStorage => Environment.GetEnvironmentVariable(nameof(this.AzureWebJobsStorage), EnvironmentVariableTarget.Process);

        public string CustomModelId => Environment.GetEnvironmentVariable(nameof(this.CustomModelId), EnvironmentVariableTarget.Process);

        public string ErrorFilesOutputContainer => Environment.GetEnvironmentVariable(nameof(this.ErrorFilesOutputContainer), EnvironmentVariableTarget.Process);

        public string ErrorReportOutputContainer => Environment.GetEnvironmentVariable(nameof(this.ErrorReportOutputContainer), EnvironmentVariableTarget.Process);

        public string FetchTranscriptionServiceBusConnectionString => Environment.GetEnvironmentVariable(nameof(this.FetchTranscriptionServiceBusConnectionString), EnvironmentVariableTarget.Process);

        public string Locale => Environment.GetEnvironmentVariable(nameof(this.Locale), EnvironmentVariableTarget.Process);

        public string ProfanityFilterMode => Environment.GetEnvironmentVariable(nameof(this.ProfanityFilterMode), EnvironmentVariableTarget.Process);

        public string PunctuationMode => Environment.GetEnvironmentVariable(nameof(this.PunctuationMode), EnvironmentVariableTarget.Process);

        public string StartTranscriptionServiceBusConnectionString => Environment.GetEnvironmentVariable(nameof(this.StartTranscriptionServiceBusConnectionString), EnvironmentVariableTarget.Process);

        public string StartTranscriptionFunctionTimeInterval => Environment.GetEnvironmentVariable(nameof(this.StartTranscriptionFunctionTimeInterval), EnvironmentVariableTarget.Process);
    }
}