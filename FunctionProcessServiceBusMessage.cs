using System;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.ApplicationInsights;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.WebJobs.ServiceBus;
using Microsoft.Data.SqlClient;
using Caresmartz.Services.SchedulerListeners.Models;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Newtonsoft.Json;
using System.IO;
using System.Text;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Caresmartz.Services.SchedulerListeners
{
    public static class SendingMessages
    {
        public const string QueueName = "firstqueue";
        public const string ServiceBusConnectionString = "ServiceBusConnectionString";
    }
    public class FunctionProcessServiceBusMessage
    {

        private static readonly TelemetryClient TelemetryClient;

        static FunctionProcessServiceBusMessage()
        {
            var config = TelemetryConfiguration.CreateDefault();
            config.InstrumentationKey = Environment.GetEnvironmentVariable("APPINSIGHTSKEY");
            TelemetryClient = new TelemetryClient(config);
        }

        [FunctionName("ProcessServiceBusSingleMessage")]
        public async Task RunProcessServiceBusSingleMessage(
            [ServiceBusTrigger(SendingMessages.QueueName, Connection = SendingMessages.ServiceBusConnectionString)] ServiceBusReceivedMessage receivedMessage, 
            ServiceBusMessageActions messageActions, ILogger log)
        {
            try
            {
                DoInsertMessageintoDatabase(receivedMessage, log);
                // Delete or forward the message after processing
                log.LogInformation($"Successfully processed normal message with ID: {receivedMessage.MessageId}");
                await messageActions.CompleteMessageAsync(receivedMessage);
            }
            catch (Exception ex)
            {
                log.LogError($"Error processing message: {ex.Message}");
                // Track exception in Application Insights
                TelemetryClient.TrackException(ex, new Dictionary<string, string> { { "MessageContent", receivedMessage.Body.ToString() } });

            }
        }

        [FunctionName("ProcessServiceBusMessageDeadLetter")]
        public async Task RunProcessServiceBusMessageDeadLetter([ServiceBusTrigger("firstqueue/$deadletterqueue", Connection = SendingMessages.ServiceBusConnectionString)]
            ServiceBusReceivedMessage deadLetterMessage, ServiceBusMessageActions messageActions, ILogger log, System.Threading.CancellationToken ct)
        {
            try
            {
                DoInsertMessageintoDatabase(deadLetterMessage, log, "DeadLetter-Run");
                // Delete or forward the message after processing
                log.LogInformation($"Successfully processed dead-letter message with ID: {deadLetterMessage.MessageId}");
                await messageActions.CompleteMessageAsync(deadLetterMessage);
            }
            catch (Exception ex)
            {
                log.LogError($"Error processing dead-letter message: {ex.Message}");
                TelemetryClient.TrackException(ex, new Dictionary<string, string> { { "MessageContent", deadLetterMessage.Body.ToString() } });
                throw; // Rethrow to retry or leave in DLQ
            }
        }
        /// <summary>
        /// Run the function to process a batch of messages for Service Bus
        /// </summary>
        /// <param name="messages"></param>
        /// <param name="messageActions"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        [FunctionName("ProcessServiceBusBatchMessages")]
        public async Task RunProcessServiceBusBatchMessages([ServiceBusTrigger(SendingMessages.QueueName, Connection = SendingMessages.ServiceBusConnectionString)] ServiceBusReceivedMessage[] messages,
            ServiceBusMessageActions messageActions, ILogger log)
        {
            log.LogInformation($"Received a batch of {messages.Length} messages for processing.");

            foreach (var receivedMessage in messages)
            {
                try
                {

                    DoInsertMessageintoDatabase(receivedMessage, log, "Batch-Run");
                    // Mark message as completed
                    await messageActions.CompleteMessageAsync(receivedMessage);
                }
                catch (Exception ex)
                {
                    log.LogError($"Error processing batch message {receivedMessage.MessageId}: {ex.Message}");
                    await messageActions.DeadLetterMessageAsync(receivedMessage, "Batch Processing Error", ex.Message);
                    log.LogError($"Error processing message: {ex.Message}");
                    // Track exception in Application Insights
                    TelemetryClient.TrackException(ex, new Dictionary<string, string> { { "MessageContent", receivedMessage.Body.ToString() } });
                    // Mark message as completed
                    //await messageActions.DeadLetterMessageAsync(receivedMessage);
                }
            }
        }
        [FunctionName("HttpServiceBusScheduleMessage")]
        public async Task<IActionResult> RunHttpServiceBusScheduleMessage([HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req, ILogger log)
        {
            log.LogInformation("Processing HTTP request to schedule a Service Bus message.");
            try
            {
                // Parse the request body
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var data = JsonConvert.DeserializeObject<ScheduleMessageRequest>(requestBody);

                if (data == null || string.IsNullOrEmpty(data.QueueOrTopicName) || string.IsNullOrEmpty(data.MessageBody) || data.ScheduleTime == null)
                {
                    return new BadRequestObjectResult("Please provide valid 'queueOrTopicName', 'messageBody', and 'scheduleTime' in the request body.");
                }
                var serviceBusConnectionString = Environment.GetEnvironmentVariable("ServiceBusConnectionString");
                var client = new ServiceBusClient(serviceBusConnectionString);
                var sender = client.CreateSender(SendingMessages.QueueName);
                try
                {

                    int agencyId = GenerateRandomAgencyId(1, 1500);
                    var messageId = Guid.NewGuid();
                    var sentTime = DateTime.UtcNow;
                    var messageBody = new
                    {
                        Id = messageId,
                        SentTimeUtc = sentTime,
                        AdditionalData = $"Message HTTP for load testing with agency Id " + agencyId.ToString(),
                        AgencyId = agencyId,
                        MessageAutoId = 1
                    };
                    var message = new ServiceBusMessage(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(messageBody)))
                    {
                        MessageId = "MessageId" + messageId.ToString(),
                        ContentType = "application/json",
                        TimeToLive = TimeSpan.FromMinutes(30),
                        PartitionKey = "AgencyId" + agencyId.ToString(),
                        ApplicationProperties =
                        {
                            // Add custom application properties
                            ["Priority"] = "High",
                            ["NotificationType"] = "Queue-Email"
                        },
                        Subject = "Scheduled Message"
                    };
                    long sequenceNumber = await sender.ScheduleMessageAsync(message, data.ScheduleTime.Value);
                    log.LogInformation($"Scheduled message for {data.QueueOrTopicName} at {data.ScheduleTime.Value}. Sequence Number: {sequenceNumber}");
                    return new OkObjectResult(new
                    {
                        Success = true,
                        Message = $"Message scheduled successfully for {data.QueueOrTopicName} at {data.ScheduleTime.Value}.",
                        SequenceNumber = sequenceNumber
                    });
                }
                catch (Exception ex)
                {
                    log.LogError($"Error scheduling message: {ex.Message}");
                    return new StatusCodeResult(StatusCodes.Status500InternalServerError);
                }
                finally
                {
                    await sender.DisposeAsync();
                    await client.DisposeAsync();
                }
            }
            catch (Exception ex)
            {
                log.LogError($"Error processing request: {ex.Message}");
                return new BadRequestObjectResult($"Error: {ex.Message}");
            }
        }


        private void DoInsertMessageintoDatabase(ServiceBusReceivedMessage receivedMessage, ILogger log, string attachedEvent = "Normal-Run")
        {
            var receivedTime = DateTime.UtcNow;
            string messageBody = receivedMessage.Body.ToString();
            var message = JsonSerializer.Deserialize<MessageModel>(messageBody);
            attachedEvent = attachedEvent + "- ";
            // Log the start of message processing
            log.LogInformation(attachedEvent + $" Received message: {message}");
            // Track the start of message processing
            TelemetryClient.TrackEvent(attachedEvent + " MessageProcessingStarted", new Dictionary<string, string> { { "MessageContent", messageBody } });

            // Access custom application properties
            if (receivedMessage.ApplicationProperties.TryGetValue("Priority", out var priorityValue))
                message.Priority = priorityValue.ToString();

            if (receivedMessage.ApplicationProperties.TryGetValue("NotificationType", out var notificationTypeValue))
                message.NotificationType = notificationTypeValue.ToString();
            // Calculate processing time
            var processingTimeMs = (int)(receivedTime - message.SentTimeUtc).TotalMilliseconds;
            // Log the details
            log.LogInformation(attachedEvent + $" Processing message with ID: {message.Id}");
            log.LogInformation(attachedEvent + $" Priority: {message.Priority}, NotificationType: {message.NotificationType}");
            log.LogInformation(attachedEvent + $" SentTime: {message.SentTimeUtc}, ReceivedTime: {receivedTime}, ProcessingTime: {processingTimeMs}ms");
            // Insert data into the SQL table
            // Fire and forget the InsertMessageAsync
            _ = Task.Run(() => InsertMessageAsync(message.Id, message.SentTimeUtc, receivedTime, processingTimeMs, message.AdditionalData, message.AgencyId, message.MessageAutoId, message.NotificationType));
            // Track successful message processing
            TelemetryClient.TrackEvent(attachedEvent + " MessageProcessingCompleted", new Dictionary<string, string> { { "MessageContent", messageBody } });
        }
        private async Task InsertMessageAsync(Guid id, DateTime sentTime, DateTime? receivedTime, int? processingTimeMs, string additionalData, int? agencyId, int? messageId, string notificationType = "Queue-Email")
        {
            try
            {
                var sqlConnectionString = Environment.GetEnvironmentVariable("SQLDbConnection");

                using (var connection = new SqlConnection(sqlConnectionString))
                {
                    await connection.OpenAsync();
                    // SQL query to insert data into the table
                    var query = @"INSERT INTO MessageTracking (Id, SentTimeUtc, ReceivedTimeUtc, ProcessingTimeMs, AdditionalData, AgencyId, MessageId, NotificationType)
                                VALUES (@Id, @SentTimeUtc, @ReceivedTimeUtc, @ProcessingTimeMs, @AdditionalData, @AgencyId, @MessageId, @NotificationType)";
                    using (var command = new SqlCommand(query, connection))
                    {
                        // Add parameters with appropriate values or handle nulls
                        command.Parameters.AddWithValue("@Id", id);
                        command.Parameters.AddWithValue("@SentTimeUtc", sentTime);
                        command.Parameters.AddWithValue("@ReceivedTimeUtc", (object?)receivedTime ?? DBNull.Value);
                        command.Parameters.AddWithValue("@ProcessingTimeMs", (object?)processingTimeMs ?? DBNull.Value);
                        command.Parameters.AddWithValue("@AdditionalData", (object?)additionalData ?? DBNull.Value);
                        command.Parameters.AddWithValue("@AgencyId", (object?)agencyId ?? DBNull.Value);
                        command.Parameters.AddWithValue("@MessageId", (object?)messageId ?? DBNull.Value);
                        command.Parameters.AddWithValue("@NotificationType", notificationType);
                        // Execute the command
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                // Log any exceptions or handle as required
                Console.WriteLine($"Error in InsertMessageAsync: {ex.Message}");
            }
        }
        int GenerateRandomAgencyId(int min, int max)
        {
            // Create an instance of Random
            Random random = new Random();
            // Generate a random number within the specified range
            return random.Next(min, max + 1);
        }
    }
}
