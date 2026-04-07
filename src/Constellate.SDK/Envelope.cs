using System;
using System.Text.Json;

namespace Constellate.SDK
{
    public enum EnvelopeType
    {
        Command,
        Event
    }

    /// <summary>
    /// RFC-040 envelope (v1.0) used for in-proc and out-of-proc messaging.
    /// </summary>
    public record Envelope
    {
        public string V { get; init; } = "1.0";
        public Guid Id { get; init; } = Guid.NewGuid();
        public DateTimeOffset Ts { get; init; } = DateTimeOffset.UtcNow;
        public EnvelopeType Type { get; init; } = EnvelopeType.Command;
        public string Name { get; init; } = string.Empty;
        public JsonElement? Payload { get; init; }
        public Guid? CorrelationId { get; init; }
    }
}
