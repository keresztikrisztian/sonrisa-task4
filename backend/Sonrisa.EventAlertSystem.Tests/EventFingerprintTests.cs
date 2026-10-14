using Sonrisa.EventAlertSystem.Api.Application;
using Xunit;

namespace Sonrisa.EventAlertSystem.Tests;

public sealed class EventFingerprintTests
{
    [Fact]
    public void Create_UsesApprovedCanonicalByteEncoding()
    {
        var fingerprint = EventFingerprint.Create(
            "  Weather\u00a0 ",
            "Flash\tFlood",
            " Road   closed ",
            DateTimeOffset.Parse("2026-05-01T12:30:45.900+02:00"));

        Assert.Equal("40c4d7fce155fcd80128379e463637922a3670a5847d9b81c573239a76ef21f3", fingerprint);
    }
}