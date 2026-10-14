using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Sonrisa.EventAlertSystem.Api.Application;

public static class EventFingerprint
{
    private static readonly byte[] VersionPrefix = [.. Encoding.ASCII.GetBytes("sonrisa-event-v1"), 0];

    public static string Create(string category, string title, string description, DateTimeOffset occurredAt)
    {
        using var stream = new MemoryStream();
        stream.Write(VersionPrefix);
        WriteText(stream, category);
        WriteText(stream, title);
        WriteText(stream, description);

        Span<byte> timestampBytes = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(timestampBytes, occurredAt.ToUnixTimeSeconds());
        stream.Write(timestampBytes);

        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    private static void WriteText(Stream stream, string value)
    {
        var normalized = NormalizeText(value);
        var bytes = Encoding.UTF8.GetBytes(normalized);
        Span<byte> lengthBytes = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32BigEndian(lengthBytes, checked((uint)bytes.Length));
        stream.Write(lengthBytes);
        stream.Write(bytes);
    }

    private static string NormalizeText(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormC);
        var result = new StringBuilder(normalized.Length);
        var pendingSpace = false;

        foreach (var rune in normalized.EnumerateRunes())
        {
            if (Rune.IsWhiteSpace(rune))
            {
                pendingSpace = result.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                result.Append(' ');
                pendingSpace = false;
            }

            result.Append(rune.ToString());
        }

        return result.ToString().ToUpperInvariant();
    }
}