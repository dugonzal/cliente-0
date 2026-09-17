using System.Net;

namespace SnmpContracts.Models;

/// <summary>SNMP protocol version to use.</summary>
public enum SnmpVersion
{
    V1 = 1,
    V2c = 2,
    V3 = 3,
}

/// <summary>USM authentication (HMAC) algorithm for SNMPv3.</summary>
public enum AuthProtocol
{
    None,
    MD5,
    SHA1,
    SHA256,
    SHA384,
    SHA512,
}

/// <summary>USM privacy (encryption) algorithm for SNMPv3.</summary>
public enum PrivProtocol
{
    None,
    DES,
    AES128,
    AES192,
    AES256,
    TripleDES,
}

/// <summary>USM security parameters for SNMPv3 (user + auth/priv credentials).</summary>
public sealed record V3Security(
    string UserName,
    AuthProtocol Auth,
    string AuthPassphrase,
    PrivProtocol Priv,
    string PrivPassphrase)
{
    /// <summary>The no-auth/no-priv ("noauthnopriv") user — plain community-less access.</summary>
    public static readonly V3Security NoAuthNoPriv =
        new(string.Empty, AuthProtocol.None, string.Empty, PrivProtocol.None, string.Empty);

    public bool HasAuth => Auth != AuthProtocol.None;
    public bool HasPriv => Priv != PrivProtocol.None;
}

/// <summary>Connection configuration for an SNMP endpoint.</summary>
public sealed record SnmpConfig(
    string Host,
    int Port = 161,
    SnmpVersion Version = SnmpVersion.V2c,
    string ReadCommunity = "public",
    string WriteCommunity = "private",
    V3Security? V3 = null,
    int TimeoutMs = 3000,
    int MaxRepetitions = 10)
{
    public IPEndPoint Endpoint => new(IPAddress.Parse(Host), Port);

    /// <summary>Convenience factory for an SNMPv3 configuration (SHA-256 + AES-128).</summary>
    public static SnmpConfig V3Config(string host, string user, string authPass, string privPass, int port = 161)
        => V3Config(host, user, AuthProtocol.SHA256, authPass, PrivProtocol.AES128, privPass, port);

    /// <summary>
    /// Convenience factory for an SNMPv3 configuration with explicit algorithms. Real gear is
    /// configured with whatever its owners installed years ago, so the algorithms have to be
    /// selectable, not assumed.
    /// </summary>
    public static SnmpConfig V3Config(string host, string user, AuthProtocol auth, string authPass,
        PrivProtocol priv, string privPass, int port = 161)
        => new(host, port, SnmpVersion.V3, V3: new V3Security(user, auth, authPass, priv, privPass));

    /// <summary>Convenience factory for a v1/v2c community configuration.</summary>
    public static SnmpConfig CommunityConfig(string host, string community, SnmpVersion version = SnmpVersion.V2c, int port = 161)
        => new(host, port, version, ReadCommunity: community);
}
