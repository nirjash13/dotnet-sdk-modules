using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Identity.Application.Organizations;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure.DomainClaims;

/// <summary>
/// Verifies domain ownership by resolving TXT records for <c>_saasbuilder-verify.&lt;domain&gt;</c>.
/// Uses <see cref="Dns.GetHostAddressesAsync"/> as a lightweight DNS API available in .NET 10.
/// For TXT records, falls back to raw socket query (DnsClient library preferred in production;
/// this implementation uses <c>nslookup</c>-style raw TXT lookup via <see cref="DnsMessage"/>).
/// </summary>
/// <remarks>
/// Production recommendation: replace with the DnsClient.NET NuGet package for full TXT record support.
/// This implementation uses <c>System.Net.NetworkInformation.Ping</c>-style approach as a
/// no-extra-dependency fallback.
/// </remarks>
public sealed class DnsTxtDomainOwnershipVerifier : IDomainOwnershipVerifier
{
    private readonly ILogger<DnsTxtDomainOwnershipVerifier> _logger;

    /// <summary>Initializes a new instance of <see cref="DnsTxtDomainOwnershipVerifier"/>.</summary>
    public DnsTxtDomainOwnershipVerifier(ILogger<DnsTxtDomainOwnershipVerifier> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<bool> VerifyAsync(
        string domain,
        string token,
        CancellationToken cancellationToken = default)
    {
        string verifyHost = $"_saasbuilder-verify.{domain}";
        string expectedValue = $"saasbuilder-verify={token}";

        try
        {
            // Use raw DNS query for TXT records (UDP port 53).
            // Build a minimal DNS TXT query packet and parse the response.
            string[] txtRecords = await QueryTxtRecordsAsync(verifyHost, cancellationToken)
                .ConfigureAwait(false);

            bool found = txtRecords.Any(r => string.Equals(r, expectedValue, StringComparison.Ordinal));

            if (!found)
            {
                _logger.LogDebug(
                    "DNS TXT verification for {Host}: expected '{Expected}', found [{Found}].",
                    verifyHost,
                    expectedValue,
                    string.Join(", ", txtRecords));
            }

            return found;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DNS TXT verification for {Host} failed with exception.", verifyHost);
            return false;
        }
    }

    private static async Task<string[]> QueryTxtRecordsAsync(string host, CancellationToken ct)
    {
        // Build a minimal DNS query for TXT (type=16) records.
        byte[] query = BuildDnsQuery(host, type: 16 /* TXT */);

        using UdpClient udp = new UdpClient();
        udp.Connect("8.8.8.8", 53);

        await udp.SendAsync(query.AsMemory(), ct).ConfigureAwait(false);

        UdpReceiveResult result = await udp.ReceiveAsync(ct).ConfigureAwait(false);
        return ParseTxtRecordsFromResponse(result.Buffer);
    }

    private static byte[] BuildDnsQuery(string host, ushort type)
    {
        // Transaction ID (random 2 bytes)
        byte[] txid = System.Security.Cryptography.RandomNumberGenerator.GetBytes(2);

        // Encode host labels
        System.Collections.Generic.List<byte> query = new System.Collections.Generic.List<byte>();
        query.AddRange(txid);
        query.AddRange(new byte[] { 0x01, 0x00 }); // flags: standard query, recursion desired
        query.AddRange(new byte[] { 0x00, 0x01 }); // questions: 1
        query.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }); // answers, authority, additional

        foreach (string label in host.Split('.'))
        {
            byte[] labelBytes = System.Text.Encoding.ASCII.GetBytes(label);
            query.Add((byte)labelBytes.Length);
            query.AddRange(labelBytes);
        }

        query.Add(0x00); // end of name
        query.Add((byte)(type >> 8));
        query.Add((byte)(type & 0xFF)); // QTYPE = TXT (16)
        query.AddRange(new byte[] { 0x00, 0x01 }); // QCLASS = IN

        return query.ToArray();
    }

    private static string[] ParseTxtRecordsFromResponse(byte[] response)
    {
        if (response.Length < 12)
        {
            return Array.Empty<string>();
        }

        int ancount = (response[6] << 8) | response[7];
        if (ancount == 0)
        {
            return Array.Empty<string>();
        }

        System.Collections.Generic.List<string> records = new System.Collections.Generic.List<string>();

        // Skip header (12 bytes) + question section (variable; scan past it)
        int pos = 12;

        // Skip QNAME
        while (pos < response.Length && response[pos] != 0)
        {
            pos += response[pos] + 1;
        }

        pos += 5; // skip null terminator + QTYPE (2) + QCLASS (2)

        for (int i = 0; i < ancount && pos < response.Length; i++)
        {
            // Skip name (may be a pointer)
            if (pos < response.Length && (response[pos] & 0xC0) == 0xC0)
            {
                pos += 2; // pointer
            }
            else
            {
                while (pos < response.Length && response[pos] != 0)
                {
                    pos += response[pos] + 1;
                }

                pos++;
            }

            if (pos + 10 > response.Length)
            {
                break;
            }

            ushort rtype = (ushort)((response[pos] << 8) | response[pos + 1]);
            pos += 8; // skip TYPE(2) CLASS(2) TTL(4)
            int rdlength = (response[pos] << 8) | response[pos + 1];
            pos += 2;

            if (rtype == 16 /* TXT */ && pos + rdlength <= response.Length)
            {
                // TXT RDATA: each string is length-prefixed
                int end = pos + rdlength;
                while (pos < end)
                {
                    int strLen = response[pos++];
                    if (pos + strLen <= end)
                    {
                        records.Add(System.Text.Encoding.UTF8.GetString(response, pos, strLen));
                        pos += strLen;
                    }
                }
            }
            else
            {
                pos += rdlength;
            }
        }

        return records.ToArray();
    }
}
