using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Caresmartz.Services.SchedulerListeners.Models
{
    public class MessageModel
    {
        public Guid Id { get; set; }
        public DateTime SentTimeUtc { get; set; }
        public string AdditionalData { get; set; }
        public string Priority { get; set; }
        public string NotificationType { get; set; }
        public string MessageTemplate { get; set; }
        public int AgencyId { get; set; }
        public int MessageAutoId { get; set; }
    }
    public class ScheduleMessageRequest
    {
        public string QueueOrTopicName { get; set; }
        public string MessageBody { get; set; }
        public DateTimeOffset? ScheduleTime { get; set; }
    }
}
