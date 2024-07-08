// <copyright file="StartTranscriptionByTimerTests.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE.md file in the project root for full license information.
// </copyright>

namespace Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus;
    using Connector;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.Timers;
    using Microsoft.EntityFrameworkCore.Query;

    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Moq.Protected;
    using StartTranscriptionByTimer;

    [TestClass]
    public class StartTranscriptionByTimerTests
    {
        private Mock<IStorageConnector> mockStorageConnector;
        private Mock<ServiceBusClient> mockStartServiceBusClient;
        private Mock<ServiceBusClient> mockFetchServiceBusClient;
        private Mock<ServiceBusReceiver> mockServiceBusReceiver;
        private Mock<ILogger> mockLogger;

        private Mock<IConfig> mockConfig;
        private StartTranscriptionByTimer startTranscriptionByTimer;

        [TestInitialize]
        public void Setup()
        {
            this.mockStorageConnector = new Mock<IStorageConnector>();
            this.mockStartServiceBusClient = new Mock<ServiceBusClient>();
            this.mockFetchServiceBusClient = new Mock<ServiceBusClient>();
            this.mockServiceBusReceiver = new Mock<ServiceBusReceiver>();
            this.mockLogger = new Mock<ILogger>();
            this.mockConfig = new Mock<IConfig>();

            this.startTranscriptionByTimer = new StartTranscriptionByTimer(
                this.mockStorageConnector.Object,
                this.mockStartServiceBusClient.Object,
                this.mockFetchServiceBusClient.Object,
                this.mockConfig.Object);
        }

        [TestMethod]
        public void RunNoMessagesReceivedLogsNoMessages()
        {
            // Arrange
            this.mockConfig.Setup(config => config.StartTranscriptionServiceBusConnectionString).Returns("Endpoint=sb://myservicebus.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=mykey");
            this.mockConfig.Setup(config => config.Locale).Returns("en-US");

            var timerInfo = new TimerInfo(
                new DailySchedule(new TimeSpan(0, 0, 5)),
                new ScheduleStatus
                {
                    Last = DateTime.UtcNow,
                    Next = DateTime.UtcNow.AddMinutes(5)
                },
                true);

            this.mockStartServiceBusClient
                .Setup(client => client.CreateReceiver(It.IsAny<string>(), It.IsAny<ServiceBusReceiverOptions>()))
                .Returns(this.mockServiceBusReceiver.Object);

            this.mockServiceBusReceiver
                .Setup(receiver => receiver.ReceiveMessagesAsync(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ServiceBusReceivedMessage>());

            // Act
            var result = this.startTranscriptionByTimer.Run(timerInfo, this.mockLogger.Object);

            // Assert
            Assert.IsTrue(result.IsCompletedSuccessfully);
            this.mockServiceBusReceiver.Verify(
                receiver => receiver.ReceiveMessagesAsync(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public void RunNoValidMessagesReceivedCallsCompleteMessage()
        {
            // Arrange
            this.mockConfig.Setup(config => config.StartTranscriptionServiceBusConnectionString).Returns("Endpoint=sb://myservicebus.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=mykey");
            this.mockConfig.Setup(config => config.Locale).Returns("en-US");

            var timerInfo = new TimerInfo(
                new DailySchedule(new TimeSpan(0, 0, 5)),
                new ScheduleStatus
                {
                    Last = DateTime.UtcNow,
                    Next = DateTime.UtcNow.AddMinutes(5)
                },
                true);

            this.mockStartServiceBusClient
                .Setup(client => client.CreateReceiver(It.IsAny<string>(), It.IsAny<ServiceBusReceiverOptions>()))
                .Returns(this.mockServiceBusReceiver.Object);

            var message = ServiceBusModelFactory.ServiceBusReceivedMessage(new BinaryData("valida data"), "123", null, null, null, null, new TimeSpan(0, 0, 7), null, null, null, "text/plain", null, DateTimeOffset.Now.Add(new TimeSpan(0, 0, 5)), null, Guid.Empty, 1, DateTimeOffset.UtcNow.AddSeconds(80));
            this.mockServiceBusReceiver
                .Setup(receiver => receiver.ReceiveMessagesAsync(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ServiceBusReceivedMessage> { message });

            this.mockServiceBusReceiver
                .Setup(receiver => receiver.CompleteMessageAsync(It.IsAny<ServiceBusReceivedMessage>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = this.startTranscriptionByTimer.Run(timerInfo, this.mockLogger.Object);

            // Assert
            Assert.IsTrue(result.IsCompletedSuccessfully);
            Assert.IsTrue(this.mockServiceBusReceiver.Invocations.Count == 2);
            this.mockServiceBusReceiver.Verify(
                receiver => receiver.ReceiveMessagesAsync(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
            this.mockServiceBusReceiver.Verify(
                receiver => receiver.CompleteMessageAsync(It.IsAny<ServiceBusReceivedMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}