// File: Services/DatabaseService.cs
using System;
using System.Collections.Generic;
using MySql.Data.MySqlClient;
using EmailTrackerBackend.Models;

namespace EmailTrackerBackend.Services
{
    public class DatabaseService
    {
        private readonly string _connStr;
        public DatabaseService(string connStr) => _connStr = connStr;

        // Insert or update email using message_id as unique key
        public void SaveEmail(Email email)
        {
            using var conn = new MySqlConnection(_connStr);
            conn.Open();

            string query = @"
                INSERT INTO emails (message_id, sender, subject, body, received_at, is_task, meeting_time)
                VALUES (@message_id, @sender, @subject, @body, @received_at, @is_task, @meeting_time)
                ON DUPLICATE KEY UPDATE
                    sender = VALUES(sender),
                    subject = VALUES(subject),
                    body = VALUES(body),
                    received_at = VALUES(received_at),
                    is_task = VALUES(is_task),
                    meeting_time = VALUES(meeting_time);";

            using var cmd = new MySqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@message_id", email.MessageId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@sender", email.Sender ?? string.Empty);
            cmd.Parameters.AddWithValue("@subject", email.Subject ?? string.Empty);
            cmd.Parameters.AddWithValue("@body", email.Body ?? string.Empty);
            cmd.Parameters.AddWithValue("@received_at", email.ReceivedAt);
            cmd.Parameters.AddWithValue("@is_task", email.IsTask);
            cmd.Parameters.AddWithValue("@meeting_time", email.MeetingTime.HasValue ? email.MeetingTime.Value : (object)DBNull.Value);

            cmd.ExecuteNonQuery();
        }

        // Quick exists check by message_id to skip downloading/parsing duplicates early
        public bool EmailExists(string messageId)
        {
            if (string.IsNullOrEmpty(messageId))
                return false;

            using var conn = new MySqlConnection(_connStr);
            conn.Open();

            string query = "SELECT 1 FROM emails WHERE message_id = @message_id LIMIT 1";
            using var cmd = new MySqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@message_id", messageId);

            using var reader = cmd.ExecuteReader();
            return reader.Read();
        }

        // Return all stored emails
        public List<Email> GetAllEmails()
        {
            var emails = new List<Email>();

            using var conn = new MySqlConnection(_connStr);
            conn.Open();

            string query = "SELECT id, message_id, sender, subject, body, received_at, is_task, meeting_time FROM emails ORDER BY received_at DESC";
            using var cmd = new MySqlCommand(query, conn);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                emails.Add(new Email
                {
                    Id = reader.GetInt32("id"),
                    MessageId = reader.IsDBNull(reader.GetOrdinal("message_id")) ? null : reader.GetString("message_id"),
                    Sender = reader.IsDBNull(reader.GetOrdinal("sender")) ? null : reader.GetString("sender"),
                    Subject = reader.IsDBNull(reader.GetOrdinal("subject")) ? null : reader.GetString("subject"),
                    Body = reader.IsDBNull(reader.GetOrdinal("body")) ? null : reader.GetString("body"),
                    ReceivedAt = reader.IsDBNull(reader.GetOrdinal("received_at")) ? DateTime.MinValue : reader.GetDateTime("received_at"),
                    IsTask = !reader.IsDBNull(reader.GetOrdinal("is_task")) && reader.GetBoolean("is_task"),
                    MeetingTime = reader.IsDBNull(reader.GetOrdinal("meeting_time")) ? (DateTime?)null : reader.GetDateTime("meeting_time")
                });
            }

            return emails;
        }
    }
}
