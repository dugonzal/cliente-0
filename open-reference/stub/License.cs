using System.Security.Cryptography;
using System.Text.Json;

namespace SnmpStub;

/// <summary>Thrown when a token is missing, malformed, tampered with, or expired.</summary>
public sealed class LicenseException : Exception
{
    public LicenseException(string message) : base(message) { }
}

/// <summary>What a token grants. Serialized into the payload and signed by the author.</summary>
public sealed record LicenseClaims(
    string Licensee,
    DateTime IssuedUtc,
    DateTime ExpiresUtc,
    string Edition = "stack");

/// <summary>
/// The gate, reproducible. The closed runtime refuses to start without an author-signed token;
/// this is that refusal, in the open, so a reader can see the door and test it.
///
/// Token format (text, three lines):
///   SNMPLICENSE v1
///   base64(claims JSON)
///   base64(ECDSA P-256 signature over the payload bytes)
///
/// What this proves: only the holder of the author's private key can mint a token. A public key
/// verifies and cannot sign, so publishing it gives nothing away — it is the whole point of the
/// scheme. Verification is offline: nothing is sent anywhere.
/// </summary>
public static class LicenseGate
{
    /// <summary>Author's ECDSA P-256 public key (X||Y, 64 bytes, base64).</summary>
    private const string PublicKeyB64 =
        "d4Y0/rbyOqgOi8UqSc5tbEM9nBbSpTP7uMZjfQvwbg8iCl2wTL8kFh3ZunSfehVvKIeXdVq127lWgOX2x0JdxA==";

    private const string Magic = "SNMPLICENSE v1";
    private const string FormatError = "Not a valid SNMPLICENSE file.";

    /// <summary>Blocks unless the token is valid. Throws <see cref="LicenseException"/> otherwise.</summary>
    public static LicenseClaims RequireValid(string? licenseText = null)
    {
        licenseText ??= Environment.GetEnvironmentVariable("SNMP_LICENSE");
        if (string.IsNullOrWhiteSpace(licenseText))
            throw new LicenseException(
                "This software is licensed to its authors. Obtain a valid license before running it. " +
                "See SNMP_LICENSE or --license <file>.");

        string[] lines = licenseText.Replace("\r\n", "\n").Trim().Split('\n');
        if (lines.Length < 3 || lines[0].Trim() != Magic)
            throw new LicenseException(FormatError);

        byte[] payload, signature;
        try
        {
            payload = Convert.FromBase64String(lines[1].Trim());
            signature = Convert.FromBase64String(lines[2].Trim());
        }
        catch (FormatException)
        {
            throw new LicenseException(FormatError);
        }

        using ECDsa ecdsa = LoadPublicKey();
        if (!ecdsa.VerifyData(payload, signature, HashAlgorithmName.SHA256))
            throw new LicenseException("License signature is invalid.");

        LicenseClaims claims = JsonSerializer.Deserialize<LicenseClaims>(payload)
            ?? throw new LicenseException(FormatError);

        if (claims.ExpiresUtc < DateTime.UtcNow)
            throw new LicenseException("License has expired.");

        return claims;
    }

    private static ECDsa LoadPublicKey()
    {
        byte[] point = Convert.FromBase64String(PublicKeyB64);
        if (point.Length != 64)
            throw new LicenseException("Embedded public key is malformed.");

        ECDsa ecdsa = ECDsa.Create();
        ecdsa.ImportParameters(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = point[..32], Y = point[32..] },
        });
        return ecdsa;
    }
}
