# **Caresmartz - Azure Service Bus Processing Functions**

![image](https://github.com/user-attachments/assets/f7ce32bb-ac40-4014-8352-8c3806b1dbc4)

This repository contains Azure Functions designed to handle various scenarios of Azure Service Bus messaging, including:

- **Single message processing**
- **Batch message processing**
- **Scheduled message sending**
- **Dead-letter queue processing**
- **SQL database logging for processed messages**


![image](https://github.com/user-attachments/assets/bf192f6c-eca6-4568-984d-c90690e57cac)


## **Overview**

The functions in this repository implement robust, scalable, and professional solutions for handling Service Bus messages. They ensure reliable delivery, exception handling, and integration with Application Insights for monitoring and diagnostics.

---

## **Functions**

### **1. ProcessServiceBusSingleMessage**
- **Description**: Processes a single message from the Service Bus queue.
- **Trigger**: Service Bus queue (`firstqueue`).
- **Key Features**:
  - Logs the message details.
  - Inserts message metadata into a SQL database.
  - Completes the message after successful processing.
  - Tracks errors and exceptions in **Application Insights**.

#### **Example Use Case**
- Processes user notifications or other single-task operations queued in Service Bus.
![image](https://github.com/user-attachments/assets/95353c93-3059-47dd-8675-b565d9e6b89b)

---

### **2. ProcessServiceBusMessageDeadLetter**
- **Description**: Processes messages from the Dead Letter Queue (DLQ).
- **Trigger**: Service Bus dead-letter queue (`firstqueue/$deadletterqueue`).
- **Key Features**:
  - Handles and logs failed messages.
  - Tracks reasons for dead-lettering (e.g., processing failures or retries exceeded).
  - Inserts metadata into a database for debugging.

#### **Example Use Case**
- Investigates and resolves issues with failed or malformed messages.
![image](https://github.com/user-attachments/assets/526cf84e-b1f7-429c-8139-386e041431e4)

---

### **3. ProcessServiceBusBatchMessages**
- **Description**: Processes multiple messages in a single batch.
- **Trigger**: Service Bus queue (`firstqueue`).
- **Key Features**:
  - Efficiently processes messages in batches to improve throughput.
  - Supports dead-lettering for individual messages that fail.
  - Tracks and logs batch processing events.

#### **Example Use Case**
- Handles high message loads, such as bulk email notifications.
![image](https://github.com/user-attachments/assets/3409fb9c-8de2-4c80-acd4-cb955cd9da4e)

---

### **4. HttpServiceBusScheduleMessage**
- **Description**: Schedules a message for future delivery to a queue or topic.
- **Trigger**: HTTP POST request.
- **Key Features**:
  - Allows scheduling messages with custom payloads and delivery times.
  - Logs successful scheduling with the sequence number.
![image](https://github.com/user-attachments/assets/22e9bf78-c056-45d5-a7b6-4788053b17fe)


#### **Helper Methods**

### **1. DoInsertMessageIntoDatabase**
- **Description**: Logs and inserts message metadata into a SQL database table.

### **2. InsertMessageAsync**
- **Description**: Handles asynchronous SQL insertion for messages.

### **3. GenerateRandomAgencyId**
- **Description**: Generates random integers for `AgencyId`, useful for testing scenarios.

---

# **How to Use**

## **1. Configure Environment Variables**
Ensure the following environment variables are set in your Azure Function App or `local.settings.json`:

- **`ServiceBusConnectionString`**: Azure Service Bus connection string.
- **`SQLDbConnection`**: SQL database connection string.
- **`APPINSIGHTSKEY`**: Application Insights instrumentation key.

---

## **2. Test the Functions**

### **A. Process Single Messages**
1. Send a message to `firstqueue`.
2. Observe logs for message processing and database entries.

### **B. Process Dead-Letter Queue**
1. Add malformed messages to `firstqueue/$deadletterqueue`.
2. Run the `ProcessServiceBusMessageDeadLetter` function.

### **C. Schedule Messages via HTTP**
1. Send an HTTP POST request to the `HttpServiceBusScheduleMessage` endpoint.

#### **Sample Request**
**http**
POST /api/HttpServiceBusScheduleMessage
Content-Type: application/json
{
    "QueueOrTopicName": "firstqueue",
    "MessageBody": "Scheduled Message",
    "ScheduleTime": "2024-12-15T10:30:00Z"
}

# **Database Schema**

The `MessageTracking` table stores processed message details:

```sql
CREATE TABLE MessageTracking (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    SentTimeUtc DATETIME NOT NULL,
    ReceivedTimeUtc DATETIME NULL,
    ProcessingTimeMs INT NULL,
    AdditionalData NVARCHAR(MAX) NULL,
    AgencyId INT NULL,
    MessageId INT NULL,
    NotificationType NVARCHAR(200) NULL
);


