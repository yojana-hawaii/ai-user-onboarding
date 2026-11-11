using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Memory;

namespace DaHub.API.Authentication;

// Deliberately process-local: a restart invalidates every session. Use one API instance.
public sealed class ServerTicketStore(TimeProvider clock) : ITicketStore, IDisposable
{
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(30);
    private readonly MemoryCache _sessions = new(new MemoryCacheOptions { SizeLimit = 10000 });

    private sealed class Session(byte[] ticket, DateTimeOffset expires)
    {
        public byte[] Ticket { get; set; } = ticket;
        public DateTimeOffset Expires { get; set; } = expires;
    }

    public Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        var key = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        _sessions.Set(key, new Session(TicketSerializer.Default.Serialize(ticket), clock.GetUtcNow() + IdleTimeout),
            new MemoryCacheEntryOptions { SlidingExpiration = IdleTimeout, Size = 1 });
        return Task.FromResult(key);
    }

    public Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        // Never recreate a session removed by a concurrent logout.
        if (_sessions.TryGetValue<Session>(key, out var session) && session is not null)
        {
            lock (session)
            {
                if (session.Expires > clock.GetUtcNow())
                    session.Ticket = TicketSerializer.Default.Serialize(ticket);
            }
        }
        return Task.CompletedTask;
    }

    public Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        if (!_sessions.TryGetValue<Session>(key, out var session) || session is null)
            return Task.FromResult<AuthenticationTicket?>(null);
        lock (session)
        {
            var now = clock.GetUtcNow();
            if (session.Expires <= now)
            {
                _sessions.Remove(key);
                return Task.FromResult<AuthenticationTicket?>(null);
            }
            session.Expires = now + IdleTimeout;
            var ticket = TicketSerializer.Default.Deserialize(session.Ticket);
            if (ticket is not null)
            {
                // Idle expiry is enforced by this store on every request, not the cookie renewal interval.
                ticket.Properties.IssuedUtc = now;
                ticket.Properties.ExpiresUtc = session.Expires;
            }
            return Task.FromResult(ticket);
        }
    }

    public Task RemoveAsync(string key)
    {
        _sessions.Remove(key);
        return Task.CompletedTask;
    }

    public void Dispose() => _sessions.Dispose();
}
