using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Anchor.Networking
{
    public static class AnchorJson
    {
        public static string BuildEnvelope(
            string type,
            string requestId = null,
            string roomId = null,
            string senderId = null,
            int? seq = null,
            double? sentAt = null,
            string schema = null,
            string payloadJson = "{}")
        {
            var fields = new List<string>
            {
                Pair("type", type)
            };

            AddOptional(fields, "requestId", requestId);
            AddOptional(fields, "roomId", roomId);
            AddOptional(fields, "senderId", senderId);
            if (seq.HasValue) fields.Add("\"seq\":" + seq.Value.ToString(CultureInfo.InvariantCulture));
            if (sentAt.HasValue) fields.Add("\"sentAt\":" + sentAt.Value.ToString("0.###", CultureInfo.InvariantCulture));
            AddOptional(fields, "schema", schema);
            fields.Add("\"payload\":" + (string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson));

            return "{" + string.Join(",", fields) + "}";
        }

        public static string BuildDemoStatePayload(Vector3 position, float rotationY, string state)
        {
            return "{"
                + "\"position\":" + BuildVector3(position) + ","
                + "\"rotationY\":" + rotationY.ToString("0.###", CultureInfo.InvariantCulture) + ","
                + Pair("state", state)
                + "}";
        }

        public static string BuildDemoEventPayload(string eventId, string eventType, string text)
        {
            return "{"
                + Pair("eventId", eventId) + ","
                + Pair("eventType", eventType) + ","
                + "\"data\":{\"text\":" + Quote(text) + "}"
                + "}";
        }

        public static string BuildVector3(Vector3 value)
        {
            return "{"
                + "\"x\":" + value.x.ToString("0.###", CultureInfo.InvariantCulture) + ","
                + "\"y\":" + value.y.ToString("0.###", CultureInfo.InvariantCulture) + ","
                + "\"z\":" + value.z.ToString("0.###", CultureInfo.InvariantCulture)
                + "}";
        }

        public static string Pair(string key, string value)
        {
            return Quote(key) + ":" + Quote(value ?? string.Empty);
        }

        public static string Quote(string value)
        {
            if (value == null) return "null";

            var builder = new StringBuilder(value.Length + 2);
            builder.Append('"');
            foreach (var c in value)
            {
                switch (c)
                {
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        builder.Append(c);
                        break;
                }
            }
            builder.Append('"');
            return builder.ToString();
        }

        public static string GetString(string json, string propertyName)
        {
            if (string.IsNullOrEmpty(json)) return null;

            var match = Regex.Match(
                json,
                "\"" + Regex.Escape(propertyName) + "\"\\s*:\\s*\"((?:\\\\.|[^\"])*)\"",
                RegexOptions.CultureInvariant);

            return match.Success ? Regex.Unescape(match.Groups[1].Value) : null;
        }

        public static float GetFloat(string json, string propertyName, float fallback = 0f)
        {
            if (string.IsNullOrEmpty(json)) return fallback;

            var match = Regex.Match(
                json,
                "\"" + Regex.Escape(propertyName) + "\"\\s*:\\s*(-?\\d+(?:\\.\\d+)?)",
                RegexOptions.CultureInvariant);

            return match.Success && float.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? value
                : fallback;
        }

        public static Vector3 GetVector3(string json, string propertyName, Vector3 fallback)
        {
            var raw = GetRawProperty(json, propertyName);
            if (string.IsNullOrEmpty(raw)) return fallback;

            return new Vector3(
                GetFloat(raw, "x", fallback.x),
                GetFloat(raw, "y", fallback.y),
                GetFloat(raw, "z", fallback.z));
        }

        public static string GetPayload(string json)
        {
            return GetRawProperty(json, "payload") ?? "{}";
        }

        public static List<string> GetRoomIds(string json)
        {
            var result = new List<string>();
            foreach (Match match in Regex.Matches(json ?? string.Empty, "\"roomId\"\\s*:\\s*\"([^\"]+)\""))
            {
                var value = match.Groups[1].Value;
                if (!result.Contains(value)) result.Add(value);
            }
            return result;
        }

        public static bool GetBool(string json, string propertyName, bool fallback = false)
        {
            if (string.IsNullOrEmpty(json)) return fallback;

            var match = Regex.Match(
                json,
                "\"" + Regex.Escape(propertyName) + "\"\\s*:\\s*(true|false)",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

            return match.Success ? string.Equals(match.Groups[1].Value, "true", StringComparison.OrdinalIgnoreCase) : fallback;
        }

        public static List<string> GetObjectArrayItems(string jsonArray)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(jsonArray)) return result;

            for (var index = 0; index < jsonArray.Length; index++)
            {
                if (jsonArray[index] != '{') continue;

                var end = FindMatching(jsonArray, index);
                if (end <= index) continue;

                result.Add(jsonArray.Substring(index, end - index + 1));
                index = end;
            }

            return result;
        }

        public static string GetRawProperty(string json, string propertyName)
        {
            if (string.IsNullOrEmpty(json)) return null;

            var key = "\"" + propertyName + "\"";
            var keyIndex = json.IndexOf(key, StringComparison.Ordinal);
            if (keyIndex < 0) return null;

            var colonIndex = json.IndexOf(':', keyIndex + key.Length);
            if (colonIndex < 0) return null;

            var start = colonIndex + 1;
            while (start < json.Length && char.IsWhiteSpace(json[start])) start++;
            if (start >= json.Length) return null;

            if (json[start] == '{' || json[start] == '[')
            {
                var end = FindMatching(json, start);
                return end > start ? json.Substring(start, end - start + 1) : null;
            }

            var cursor = start;
            var inString = false;
            while (cursor < json.Length)
            {
                var c = json[cursor];
                if (c == '"' && (cursor == 0 || json[cursor - 1] != '\\')) inString = !inString;
                if (!inString && (c == ',' || c == '}')) break;
                cursor++;
            }

            return json.Substring(start, cursor - start).Trim();
        }

        private static void AddOptional(List<string> fields, string key, string value)
        {
            if (!string.IsNullOrEmpty(value)) fields.Add(Pair(key, value));
        }

        private static int FindMatching(string json, int start)
        {
            var open = json[start];
            var close = open == '{' ? '}' : ']';
            var depth = 0;
            var inString = false;

            for (var index = start; index < json.Length; index++)
            {
                var c = json[index];
                if (c == '"' && (index == 0 || json[index - 1] != '\\')) inString = !inString;
                if (inString) continue;

                if (c == open) depth++;
                if (c == close) depth--;
                if (depth == 0) return index;
            }

            return -1;
        }
    }
}
