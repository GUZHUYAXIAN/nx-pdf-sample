using System;

namespace NxDrawingPdfExporter.Probe
{
    /// <summary>
    /// Fixed-shape JSON payload proving the Gate 1 facts. Hand-rolled serializer:
    /// net48 probe must not take extra dependencies.
    /// </summary>
    internal sealed class ProbeReport
    {
        public string ProtocolVersion { get; } = "1";

        public string ProbeKind { get; } = "nx-session-probe";

        public string UtcTimestamp { get; set; } = "";

        public string ProcessBitness { get; set; } = "";

        public string RuntimeVersion { get; set; } = "";

        public string TargetFramework { get; } = "net48";

        public string CurrentDirectory { get; set; } = "";

        public string[] ArgumentEcho { get; set; } = new string[0];

        public bool NxSessionAvailable { get; set; }

        public bool UfSessionAvailable { get; set; }

        public string NxRootEnvironment { get; set; } = "";

        public int ExitCode { get; set; }

        public string Error { get; set; } = "";

        public string ToJson()
        {
            var members = new System.Collections.Generic.List<string>
            {
                Member("protocolVersion", Quote(ProtocolVersion)),
                Member("probeKind", Quote(ProbeKind)),
                Member("utcTimestamp", Quote(UtcTimestamp)),
                Member("processBitness", Quote(ProcessBitness)),
                Member("runtimeVersion", Quote(RuntimeVersion)),
                Member("targetFramework", Quote(TargetFramework)),
                Member("currentDirectory", Quote(CurrentDirectory)),
                Member("argumentEcho", ArrayJson(ArgumentEcho)),
                Member("nxSessionAvailable", NxSessionAvailable ? "true" : "false"),
                Member("ufSessionAvailable", UfSessionAvailable ? "true" : "false"),
                Member("nxRootEnvironment", Quote(NxRootEnvironment)),
                Member("exitCode", ExitCode.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                Member("error", Quote(Error))
            };

            return "{\r\n  " + string.Join(",\r\n  ", members) + "\r\n}\r\n";
        }

        private static string Member(string name, string jsonValue)
        {
            return "\"" + name + "\": " + jsonValue;
        }

        private static string ArrayJson(string[] values)
        {
            var quoted = new System.Collections.Generic.List<string>();
            foreach (var value in values ?? new string[0])
            {
                quoted.Add(Quote(value ?? ""));
            }

            return "[" + string.Join(", ", quoted) + "]";
        }

        private static string Quote(string value)
        {
            var escaped = new System.Text.StringBuilder(value.Length + 8);
            escaped.Append('"');
            foreach (var ch in value)
            {
                switch (ch)
                {
                    case '\\':
                        escaped.Append("\\\\");
                        break;
                    case '"':
                        escaped.Append("\\\"");
                        break;
                    case '\b':
                        escaped.Append("\\b");
                        break;
                    case '\f':
                        escaped.Append("\\f");
                        break;
                    case '\n':
                        escaped.Append("\\n");
                        break;
                    case '\r':
                        escaped.Append("\\r");
                        break;
                    case '\t':
                        escaped.Append("\\t");
                        break;
                    default:
                        if (ch < ' ')
                        {
                            escaped.Append("\\u").Append(((int)ch).ToString("x4", System.Globalization.CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            escaped.Append(ch);
                        }

                        break;
                }
            }

            escaped.Append('"');
            return escaped.ToString();
        }
    }
}
