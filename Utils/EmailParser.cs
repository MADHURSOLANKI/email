using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Recognizers.Text;
using Microsoft.Recognizers.Text.DateTime;

namespace EmailTrackerBackend.Utils
{
    public static class EmailParser
    {
        // ✅ Detect if the email is about a meeting
        public static bool IsMeeting(string body)
        {
            if (string.IsNullOrEmpty(body)) return false;
            var lower = body.ToLower();
            return lower.Contains("meeting") || lower.Contains("schedule") || lower.Contains("appointment");
        }

        // ✅ Extract meeting time using Microsoft Recognizers
        public static DateTime? ExtractMeetingTime(string body)
        {
            if (string.IsNullOrEmpty(body)) return null;

            // Recognize datetime expressions in English
            var results = DateTimeRecognizer.RecognizeDateTime(body, Culture.English);

            if (results == null || results.Count == 0)
                return null;

            // Loop over results and extract first valid DateTime
            foreach (var result in results)
            {
                if (result.Resolution != null && result.Resolution.TryGetValue("values", out object valuesObj))
                {
                    var values = valuesObj as List<Dictionary<string, string>>;
                    if (values != null)
                    {
                        foreach (var valueDict in values)
                        {
                            if (valueDict.TryGetValue("value", out string val) &&
                                DateTime.TryParse(val, out DateTime parsed))
                            {
                                // only accept today or future (skip old references like "yesterday")
                                if (parsed >= DateTime.Now.Date)
                                    return parsed;
                            }
                        }
                    }
                }
            }

            return null; // no valid datetime found
        }
    }
}
