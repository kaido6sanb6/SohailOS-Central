namespace SohailOS.Core;

public interface IApprovalReplayGuard
{
    bool TryConsume(string nonce, DateTimeOffset expiresAt);
}

public sealed class InMemoryApprovalReplayGuard : IApprovalReplayGuard
{
    private readonly HashSet<string> _consumed = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public bool TryConsume(string nonce, DateTimeOffset expiresAt)
    {
        if (string.IsNullOrWhiteSpace(nonce) || expiresAt <= DateTimeOffset.UtcNow)
            return false;

        lock (_gate)
        {
            if (_consumed.Contains(nonce))
                return false;

            _consumed.Add(nonce);
            return true;
        }
    }
}
