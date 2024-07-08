// <copyright file="StartTranscriptionHelper.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE.md file in the project root for full license information.
// </copyright>

namespace StartTranscriptionByTimer
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus;
    using Connector;
    using Connector.Serializable.TranscriptionStartedServiceBusMessage;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Storage;
    using Newtonsoft.Json;

    public class StartTranscriptionHelper
    {
        // private static readonly ServiceBusClient StartServiceBusClient = new ServiceBusClient(StartTranscriptionEnvironmentVariables.StartTranscriptionServiceBusConnectionString);

        // private static readonly ServiceBusSender StartSender = StartServiceBusClient.CreateSender(ServiceBusConnectionStringProperties.Parse(StartTranscriptionEnvironmentVariables.StartTranscriptionServiceBusConnectionString).EntityPath);

        // private static readonly ServiceBusClient FetchServiceBusClient = new ServiceBusClient(StartTranscriptionEnvironmentVariables.FetchTranscriptionServiceBusConnectionString);

        // private static readonly ServiceBusSender FetchSender = FetchServiceBusClient.CreateSender(ServiceBusConnectionStringProperties.Parse(StartTranscriptionEnvironmentVariables.FetchTranscriptionServiceBusConnectionString).EntityPath);
        private readonly ServiceBusClient fetchServiceBusClient;

        private readonly ServiceBusClient startServiceBusClient;

        // private readonly string subscriptionKey = StartTranscriptionEnvironmentVariables.AzureSpeechServicesKey;
        // private readonly string errorReportContaineName = StartTranscriptionEnvironmentVariables.ErrorReportOutputContainer;
        // private readonly string audioInputContainerName = StartTranscriptionEnvironmentVariables.AudioInputContainer;
        // private readonly int filesPerTranscriptionJob = StartTranscriptionEnvironmentVariables.FilesPerTranscriptionJob;
        private readonly ILogger logger;

        private readonly string locale;

        private readonly IStorageConnector storageConnector;

        private readonly IConfig config;

        public StartTranscriptionHelper(ILogger logger, IStorageConnector storageConnector, ServiceBusClient startServiceBusClient, ServiceBusClient fetchServiceBusClient, IConfig config)
        {
            this.logger = logger;
            this.storageConnector = storageConnector;
            this.startServiceBusClient = startServiceBusClient;
            this.fetchServiceBusClient = fetchServiceBusClient;
            this.config = config;
            this.locale = this.config.Locale.Split('|')[0].Trim();
        }

        public async Task StartTranscriptionsAsync(IEnumerable<ServiceBusReceivedMessage> messages, ServiceBusReceiver messageReceiver, DateTime startDateTime)
        {
            if (messageReceiver == null)
            {
                throw new ArgumentNullException(nameof(messageReceiver));
            }

            var chunkedMessages = new List<List<ServiceBusReceivedMessage>>();
            var messageCount = messages.Count();

            for (int i = 0; i < messageCount; i += this.config.FilesPerTranscriptionJob)
            {
                var chunk = messages.Skip(i).Take(Math.Min(this.config.FilesPerTranscriptionJob, messageCount - i)).ToList();
                chunkedMessages.Add(chunk);
            }

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            for (var i = 0; i < chunkedMessages.Count; i++)
            {
                var jobName = $"{startDateTime.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)}_{i}";
                var chunk = chunkedMessages.ElementAt(i);
                await this.StartBatchTranscriptionJobAsync(chunk, jobName).ConfigureAwait(false);

                // Complete messages in batches of 10, process each batch in parallel:
                var messagesInChunk = chunk.Count;
                for (var j = 0; j < messagesInChunk; j += 10)
                {
                    var completionBatch = chunk.Skip(j).Take(Math.Min(10, messagesInChunk - j));
                    var completionTasks = completionBatch.Select(sb => messageReceiver.CompleteMessageAsync(sb));
                    await Task.WhenAll(completionTasks).ConfigureAwait(false);
                }

                // only renew lock after 2 minutes
                if (stopwatch.Elapsed.TotalSeconds > 120)
                {
                    foreach (var remainingChunk in chunkedMessages.Skip(i + 1))
                    {
                        foreach (var message in remainingChunk)
                        {
                            await messageReceiver.RenewMessageLockAsync(message).ConfigureAwait(false);
                        }
                    }

                    stopwatch.Restart();
                }

                // Delay here to avoid throttling
                await Task.Delay(500).ConfigureAwait(false);
            }
        }

        public async Task StartTranscriptionAsync(ServiceBusReceivedMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            var busMessage = JsonConvert.DeserializeObject<Connector.ServiceBusMessage>(message.Body.ToString());
            var audioFileName = this.storageConnector.GetFileNameFromUri(busMessage.Data.Url);

            await this.StartBatchTranscriptionJobAsync(new[] { message }, audioFileName).ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Catch general exception to ensure that job continues execution even if message is invalid.")]
        public bool IsValidServiceBusMessage(ServiceBusReceivedMessage message)
        {
            if (message == null || message.Body == null)
            {
                this.logger.LogError($"Message {nameof(message)} is null.");
                return false;
            }

            var messageBody = message.Body.ToString();

            try
            {
                var serviceBusMessage = JsonConvert.DeserializeObject<Connector.ServiceBusMessage>(messageBody);

                if (serviceBusMessage.EventType.Contains("BlobCreate", StringComparison.OrdinalIgnoreCase) &&
                    this.storageConnector.GetContainerNameFromUri(serviceBusMessage.Data.Url).Equals(this.config.AudioInputContainer, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            catch (Exception e)
            {
                this.logger.LogError($"Exception {e.Message} while parsing message {messageBody} - message will be ignored.");
                return false;
            }

            return false;
        }

        private static TimeSpan GetMessageDelayTime(int pollingCounter, IConfig config)
        {
            if (pollingCounter == 0)
            {
                return TimeSpan.FromMinutes(config.InitialPollingDelayInMinutes);
            }

            var updatedDelay = Math.Pow(2, Math.Min(pollingCounter, 8)) * config.InitialPollingDelayInMinutes;

            if ((int)updatedDelay > config.MaxPollingDelayInMinutes)
            {
                return TimeSpan.FromMinutes(config.MaxPollingDelayInMinutes);
            }

            return TimeSpan.FromMinutes(updatedDelay);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Allow general exception catching to retry transcriptions in that case.")]
        private async Task StartBatchTranscriptionJobAsync(IEnumerable<ServiceBusReceivedMessage> messages, string jobName)
        {
            if (messages == null || !messages.Any())
            {
                this.logger.LogError($"Invalid service bus message(s).");
                return;
            }

            var locationString = string.Empty;
            var serviceBusMessages = messages.Select(message => JsonConvert.DeserializeObject<Connector.ServiceBusMessage>(Encoding.UTF8.GetString(message.Body)));

            try
            {
                var properties = this.GetTranscriptionPropertyBag();

                // only needed to make sure we do not add the same uri twice:
                var absoluteUrls = new HashSet<string>();

                var audioUrls = new List<string>();
                var audioFileInfos = new List<AudioFileInfo>();

                foreach (var serviceBusMessage in serviceBusMessages)
                {
                    var absoluteAudioUrl = serviceBusMessage.Data.Url.AbsoluteUri;

                    if (absoluteUrls.Contains(absoluteAudioUrl))
                    {
                        this.logger.LogError($"Unexpectedly received the same audio file twice: {absoluteAudioUrl}");
                        continue;
                    }

                    absoluteUrls.Add(absoluteAudioUrl);

                    if (this.config.IsByosEnabledSubscription)
                    {
                        audioUrls.Add(absoluteAudioUrl);
                    }
                    else
                    {
                        audioUrls.Add(this.storageConnector.CreateSas(serviceBusMessage.Data.Url));
                    }

                    var fileName = this.storageConnector.GetFileNameFromUri(new Uri(absoluteAudioUrl));

                    audioFileInfos.Add(new AudioFileInfo(absoluteAudioUrl, serviceBusMessage.RetryCount, textAnalyticsRequests: null, fileName));
                }

                ModelIdentity modelIdentity = null;

                if (Guid.TryParse(this.config.CustomModelId, out var customModelId))
                {
                    modelIdentity = new ModelIdentity($"{this.config.AzureSpeechServicesEndpointUri}speechtotext/v3.0/models/{customModelId}");
                }

                var transcriptionDefinition = TranscriptionDefinition.Create(jobName, "StartByTimerTranscription", this.locale, audioUrls, properties, modelIdentity);

                var transcriptionLocation = await BatchClient.PostTranscriptionAsync(
                    transcriptionDefinition,
                    this.config.AzureSpeechServicesEndpointUri,
                    this.config.AzureSpeechServicesKey).ConfigureAwait(false);

                this.logger.LogInformation($"Location: {transcriptionLocation}");

                var transcriptionMessage = new TranscriptionStartedMessage(
                    transcriptionLocation.AbsoluteUri,
                    jobName,
                    this.locale,
                    modelIdentity != null,
                    audioFileInfos,
                    0,
                    0);

                var fetchSender = this.fetchServiceBusClient.CreateSender(
                    ServiceBusConnectionStringProperties.Parse(this.config.FetchTranscriptionServiceBusConnectionString).EntityPath);

                var fetchingDelay = TimeSpan.FromMinutes(this.config.InitialPollingDelayInMinutes);
                await ServiceBusUtilities.SendServiceBusMessageAsync(fetchSender, transcriptionMessage.CreateMessageString(), this.logger, fetchingDelay).ConfigureAwait(false);
            }
            catch (TransientFailureException e)
            {
                await this.RetryOrFailMessagesAsync(messages, $"Exception in job {jobName}: {e.Message}", isThrottled: false).ConfigureAwait(false);
            }
            catch (TimeoutException e)
            {
                await this.RetryOrFailMessagesAsync(messages, $"Exception in job {jobName}: {e.Message}", isThrottled: false).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                HttpStatusCode? httpStatusCode = null;
                if (e is HttpStatusCodeException statusCodeException && statusCodeException.HttpStatusCode.HasValue)
                {
                    httpStatusCode = statusCodeException.HttpStatusCode.Value;
                }
                else if (e is WebException webException && webException.Response != null)
                {
                    httpStatusCode = ((HttpWebResponse)webException.Response).StatusCode;
                }

                if (httpStatusCode.HasValue && httpStatusCode.Value.IsRetryableStatus())
                {
                    await this.RetryOrFailMessagesAsync(messages, $"Error in job {jobName}: {e.Message}", isThrottled: httpStatusCode.Value == HttpStatusCode.TooManyRequests).ConfigureAwait(false);
                }
                else
                {
                    await this.WriteFailedJobLogToStorageAsync(serviceBusMessages, $"Exception {e} in job {jobName}: {e.Message}", jobName).ConfigureAwait(false);
                }
            }

            this.logger.LogInformation($"Fetch transcription queue successfully informed about job at: {jobName}");
        }

        private async Task RetryOrFailMessagesAsync(IEnumerable<ServiceBusReceivedMessage> messages, string errorMessage, bool isThrottled)
        {
            this.logger.LogError(errorMessage);
            var startSender = this.startServiceBusClient.CreateSender(
                ServiceBusConnectionStringProperties.Parse(this.config.StartTranscriptionServiceBusConnectionString).EntityPath);

            foreach (var message in messages)
            {
                var serviceBusMessage = JsonConvert.DeserializeObject<Connector.ServiceBusMessage>(Encoding.UTF8.GetString(message.Body));

                if (serviceBusMessage.RetryCount <= this.config.RetryLimit || isThrottled)
                {
                    serviceBusMessage.RetryCount += 1;
                    var messageDelay = GetMessageDelayTime(serviceBusMessage.RetryCount, this.config);
                    Console.WriteLine($"Retrying message sfjnmsd {serviceBusMessage.RetryCount} times");
                    var newMessage = new Azure.Messaging.ServiceBus.ServiceBusMessage(JsonConvert.SerializeObject(serviceBusMessage));
                    await ServiceBusUtilities.SendServiceBusMessageAsync(startSender, newMessage, this.logger, messageDelay).ConfigureAwait(false);
                }
                else
                {
                    var fileName = this.storageConnector.GetFileNameFromUri(serviceBusMessage.Data.Url);
                    var errorFileName = fileName + ".txt";
                    var retryExceededErrorMessage = $"Exceeded retry count for transcription {fileName} with error message {errorMessage}.";
                    this.logger.LogError(retryExceededErrorMessage);
                    await this.ProcessFailedFileAsync(fileName, errorMessage, errorFileName).ConfigureAwait(false);
                }
            }
        }

        private async Task WriteFailedJobLogToStorageAsync(IEnumerable<Connector.ServiceBusMessage> serviceBusMessages, string errorMessage, string jobName)
        {
            this.logger.LogError(errorMessage);
            var jobErrorFileName = $"jobs/{jobName}.txt";
            await this.storageConnector.WriteTextFileToBlobAsync(errorMessage, this.config.ErrorReportOutputContainer, jobErrorFileName).ConfigureAwait(false);

            foreach (var message in serviceBusMessages)
            {
                var fileName = this.storageConnector.GetFileNameFromUri(message.Data.Url);
                var errorFileName = fileName + ".txt";
                await this.ProcessFailedFileAsync(fileName, errorMessage, errorFileName).ConfigureAwait(false);
            }
        }

        private Dictionary<string, string> GetTranscriptionPropertyBag()
        {
            var properties = new Dictionary<string, string>();

            var profanityFilterMode = this.config.ProfanityFilterMode;
            properties.Add("ProfanityFilterMode", profanityFilterMode);
            this.logger.LogInformation($"Setting profanity filter mode to {profanityFilterMode}");

            var punctuationMode = this.config.PunctuationMode;
            punctuationMode = punctuationMode.Replace(" ", string.Empty, StringComparison.Ordinal);
            properties.Add("PunctuationMode", punctuationMode);
            this.logger.LogInformation($"Setting punctuation mode to {punctuationMode}");

            var addDiarization = this.config.AddDiarization;
            properties.Add("DiarizationEnabled", addDiarization.ToString(CultureInfo.InvariantCulture));
            this.logger.LogInformation($"Setting diarization enabled to {addDiarization}");

            var addWordLevelTimestamps = this.config.AddWordLevelTimestamps;
            properties.Add("WordLevelTimestampsEnabled", addWordLevelTimestamps.ToString(CultureInfo.InvariantCulture));
            this.logger.LogInformation($"Setting word level timestamps enabled to {addWordLevelTimestamps}");

            return properties;
        }

        private async Task ProcessFailedFileAsync(string fileName, string errorMessage, string logFileName)
        {
            try
            {
                await this.storageConnector.WriteTextFileToBlobAsync(errorMessage, this.config.ErrorReportOutputContainer, logFileName).ConfigureAwait(false);
                await this.storageConnector.MoveFileAsync(
                    this.config.AudioInputContainer,
                    fileName,
                    this.config.ErrorFilesOutputContainer,
                    fileName,
                    false).ConfigureAwait(false);
            }
            catch (StorageException e)
            {
                this.logger.LogError($"Storage Exception {e} while writing error log to file and moving result");
            }
        }
    }
}
