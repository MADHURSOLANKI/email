using System;
using System.Text.RegularExpressions;
using MailKit.Net.Imap;
using MailKit;
using MailKit.Search;
using MimeKit;
using EmailTrackerBackend.Models;
using EmailTrackerBackend.Utils;

namespace EmailTrackerBackend.Services
{
    public class EmailService
    {
        private readonly string _host;
        private readonly int _port;
        private readonly string _username;
        private readonly string _password;
        private readonly DatabaseService _dbService;

        public EmailService(string host, int port, string username, string password, DatabaseService dbService)
        {
            _host = host;
            _port = port;
            _username = username;
            _password = password;
            _dbService = dbService;
        }

        /// <summary>
        /// Fetch only NEW emails after the last saved one.
        /// If no emails in DB, fetch last 24 hours.
        /// </summary>
        public void FetchEmails()
        {
            try
            {
                using var client = new ImapClient();
                client.Connect(_host, _port, true);
                client.Authenticate(_username, _password);

                var inbox = client.Inbox;
                inbox.Open(FolderAccess.ReadOnly);

                // ✅ Get last saved email time from DB
                var lastReceivedAt = _dbService.GetLastReceivedAt();

                DateTime sinceUtc;
                if (lastReceivedAt.HasValue)
                {
                    sinceUtc = lastReceivedAt.Value.ToUniversalTime();
                }
                else
                {
                    // If no emails in DB, fetch last 24 hours as initial sync
                    sinceUtc = DateTime.UtcNow.AddDays(-1);
                }

                Console.WriteLine($"[EmailService] Fetching emails after: {sinceUtc}");

                // ✅ Ask Gmail for only new emails
                var uids = inbox.Search(SearchQuery.DeliveredAfter(sinceUtc));

                foreach (var uid in uids)
                {
                    try
                    {
                        var message = inbox.GetMessage(uid);

                        // Prefer plain text; fallback to simple stripped HTML
                        string bodyText = message.TextBody;
                        if (string.IsNullOrWhiteSpace(bodyText) && !string.IsNullOrWhiteSpace(message.HtmlBody))
                        {
                            bodyText = Regex.Replace(message.HtmlBody, "<[^>]+>", " ");
                        }
                        if (string.IsNullOrWhiteSpace(bodyText))
                            bodyText = string.Empty;

                        // Check if looks like a meeting
                        if (!EmailParser.IsMeeting(bodyText))
                            continue;

                        // Extract meeting time if present
                        DateTime? meetingTime = EmailParser.ExtractMeetingTime(bodyText);

                        var email = new Email
                        {
                            MessageId = message.MessageId, // ✅ unique ID for deduplication
                            Sender = message.From?.ToString() ?? string.Empty,
                            Subject = message.Subject ?? string.Empty,
                            Body = bodyText,
                            ReceivedAt = message.Date.DateTime,
                            IsTask = true,
                            MeetingTime = meetingTime
                        };

                        _dbService.SaveEmail(email);
                    }
                    catch (Exception exPerMessage)
                    {
                        Console.WriteLine($"[EmailService] Skipping message due to error: {exPerMessage.Message}");
                    }
                }

                client.Disconnect(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EmailService] FetchEmails failed: {ex.Message}");
                throw;
            }
        }
    }
}
