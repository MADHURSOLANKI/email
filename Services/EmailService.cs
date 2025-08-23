// File: Services/EmailService.cs
using System;
using System.Text.RegularExpressions;
using System.Linq;
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
        /// Efficiently fetch only last 24h of emails:
        /// 1) fetch UIDs since last 24h,
        /// 2) batch-fetch headers (envelope),
        /// 3) skip duplicates by message-id,
        /// 4) download body only for likely meeting emails,
        /// 5) parse meeting time and save.
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

                var sinceUtc = DateTime.UtcNow.AddDays(-1);
                var uids = inbox.Search(SearchQuery.DeliveredAfter(sinceUtc));

                if (uids == null || uids.Count == 0)
                {
                    Console.WriteLine("[EmailService] No messages in the last 24 hours.");
                    client.Disconnect(true);
                    return;
                }

                // Batch fetch envelope + unique id (fast)
                var summaries = inbox.Fetch(uids, MessageSummaryItems.Envelope | MessageSummaryItems.UniqueId);

                foreach (var summary in summaries)
                {
                    try
                    {
                        // Get basic header fields without downloading body
                        var envelope = summary.Envelope;
                        string subject = envelope?.Subject ?? string.Empty;
                        string from = envelope?.From?.FirstOrDefault()?.ToString() ?? string.Empty;
                        string messageId = envelope?.MessageId ?? string.Empty;

                        // If no messageId, produce fallback key
                        if (string.IsNullOrEmpty(messageId))
                        {
                            // fallback uses unique id ticks and subject hash — deterministic for same mail fetch
                            messageId = $"{summary.UniqueId.Id}_{subject.GetHashCode()}";
                        }

                        // Skip duplicate by checking DB (fast)
                        if (_dbService.EmailExists(messageId))
                        {
                            // already stored
                            continue;
                        }

                        // Quick subject-level filter to avoid downloading many bodies
                        var subjLower = subject.ToLowerInvariant();
                        if (!(subjLower.Contains("meeting") || subjLower.Contains("schedule") || subjLower.Contains("appointment") || subjLower.Contains("invite") || subjLower.Contains("calendar")))
                        {
                            // not likely a meeting based on subject → skip body download
                            continue;
                        }

                        // Now download full message only for candidates
                        var message = inbox.GetMessage(summary.UniqueId);

                        // Prefer text body, fallback to stripped HTML
                        string bodyText = message.TextBody;
                        if (string.IsNullOrWhiteSpace(bodyText) && !string.IsNullOrWhiteSpace(message.HtmlBody))
                        {
                            bodyText = Regex.Replace(message.HtmlBody, "<[^>]+>", " ");
                        }
                        bodyText = bodyText ?? string.Empty;

                        // Double-check body for meeting indicators
                        if (!EmailParser.IsMeeting(bodyText))
                            continue;

                        // Extract meeting time (uses your EmailParser implementation)
                        DateTime? meetingTime = EmailParser.ExtractMeetingTime(bodyText);

                        // build Email model
                        var email = new Email
                        {
                            MessageId = messageId,
                            Sender = from,
                            Subject = subject,
                            Body = bodyText,
                            ReceivedAt = message.Date.DateTime,
                            IsTask = true,
                            MeetingTime = meetingTime
                        };

                        // Save (SaveEmail uses ON DUPLICATE KEY UPDATE)
                        _dbService.SaveEmail(email);

                        Console.WriteLine($"[EmailService] Saved: {subject} ({messageId})");
                    }
                    catch (Exception exPer)
                    {
                        Console.WriteLine($"[EmailService] per-message error: {exPer.Message}");
                        // continue with next summary
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
