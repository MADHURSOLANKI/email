using System;
using System.Collections.Generic;
using MySql.Data.MySqlClient;
using EmailTrackerBackend.Models;

namespace EmailTrackerBackend.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString;

        public DatabaseService(string connectionString)
        {
            _connectionString = connectionString;
        }

        // ✅ Save email only if not already in DB (based on message_id)
        public void SaveEmail(Email email)
        {
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();

            string query = @"
                INSERT INTO emails (message_id, sender, subject, body, received_at, is_task, meeting_time)
                VALUES (@MessageId, @Sender, @Subject, @Body, @ReceivedAt, @IsTask, @MeetingTime)
                ON DUPLICATE KEY UPDATE message_id = message_id;";

            using var cmd = new MySqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@MessageId", email.MessageId ?? "");
            cmd.Parameters.AddWithValue("@Sender", email.Sender ?? "");
            cmd.Parameters.AddWithValue("@Subject", email.Subject ?? "");
            cmd.Parameters.AddWithValue("@Body", email.Body ?? "");
            cmd.Parameters.AddWithValue("@ReceivedAt", email.ReceivedAt);
            cmd.Parameters.AddWithValue("@IsTask", email.IsTask);
            cmd.Parameters.AddWithValue("@MeetingTime", email.MeetingTime.HasValue ? email.MeetingTime.Value : (object)DBNull.Value);

            cmd.ExecuteNonQuery();
        }

        // ✅ Get all saved emails
        public List<Email> GetAllEmails()
        {
            var emails = new List<Email>();
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();

            string query = "SELECT * FROM emails ORDER BY received_at DESC";
            using var cmd = new MySqlCommand(query, connection);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                int meetingTimeIndex = reader.GetOrdinal("meeting_time");

                emails.Add(new Email
                {
                    Id = reader.GetInt32("id"),
                    MessageId = reader.GetString("message_id"),
                    Sender = reader.GetString("sender"),
                    Subject = reader.GetString("subject"),
                    Body = reader.GetString("body"),
                    ReceivedAt = reader.GetDateTime("received_at"),
                    IsTask = reader.GetBoolean("is_task"),
                    MeetingTime = reader.IsDBNull(meetingTimeIndex) ? null : reader.GetDateTime(meetingTimeIndex)
                });
            }

            return emails;
        }

        // ✅ Get latest received_at timestamp from DB
        public DateTime? GetLastReceivedAt()
        {
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();

            string query = "SELECT MAX(received_at) FROM emails";
            using var cmd = new MySqlCommand(query, connection);

            var result = cmd.ExecuteScalar();
            if (result == DBNull.Value || result == null)
                return null;

            return Convert.ToDateTime(result);
        }
    }
}
