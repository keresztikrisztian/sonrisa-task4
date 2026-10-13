namespace Sonrisa.EventAlertSystem.Api.Application;

public sealed record DemoIdentity(string Id, string DisplayName, string Role);

public sealed class DemoIdentityRegistry
{
    private static readonly IReadOnlyList<DemoIdentity> Identities =
    [
        new("demo-user", "Demo User", "User"),
        new("demo-admin", "Demo Administrator", "Admin")
    ];

    public IReadOnlyList<DemoIdentity> GetAll() => Identities;

    public bool TryGet(string id, out DemoIdentity? identity)
    {
        identity = Identities.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, id, StringComparison.OrdinalIgnoreCase));
        return identity is not null;
    }
}