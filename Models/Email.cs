// File: Models/Email.cs
using System;

namespace EmailTrackerBackend.Models
{
    public class Email
    {
        public int Id { get; set; }
        public string MessageId { get; set; }            // NEW: unique id from email headers (or fallback)
        public string Sender { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
        public DateTime ReceivedAt { get; set; }
        public bool IsTask { get; set; }
        public DateTime? MeetingTime { get; set; }
    }
}
