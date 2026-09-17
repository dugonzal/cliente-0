using SnmpContracts.Telemetry;
using SnmpStub;

// SnmpStub — the license gate, and nothing else.
//
// This is not the product. The management engine, the driver, the simulator and the license guard
// ship as compiled binaries; here you get the door they sit behind, in the open, so you can run it,
// read it, and see exactly what "licensed to its authors" means in practice.
//
//   dotnet run                          -> refuses: no token
//   SNMP_LICENSE="$(cat my.lic)" dotnet run
//   dotnet run -- --license my.lic      -> prints the claims, then one sample frame
//
// Exit codes: 0 valid token, 2 refused by the gate, 1 bad usage.

string? licensePath = null;
for (int i = 0; i < args.Length; i++)
    if (args[i] == "--license" && i + 1 < args.Length)
        licensePath = args[++i];
if (args.Length > 0 && licensePath is null)
{
    Console.Error.WriteLine("usage: SnmpStub [--license <file>]");
    return 1;
}

try
{
    string? text = licensePath is not null ? File.ReadAllText(licensePath) : null;
    LicenseClaims claims = LicenseGate.RequireValid(text);
    Console.WriteLine($"license OK · {claims.Licensee} · edition {claims.Edition} · valid until {claims.ExpiresUtc:u}");
}
catch (LicenseException ex)
{
    Console.Error.WriteLine($"License check failed: {ex.Message}");
    return 2;
}
catch (IOException ex)
{
    Console.Error.WriteLine($"License check failed: cannot read the token ({ex.Message}).");
    return 2;
}

// Past the gate: the open contracts, doing what they do. Three telecontrol points from one RTU.
Iec104Frame frame = new(
    SendSeq: 7,
    RecvSeq: 12,
    CommonAddress: 1,
    CauseOfTransmission: 3,      // spontaneous
    Points:
    [
        new Iec104Point(InfoObjectAddress: 16_385, IsSinglePoint: true, Value: 1),
        new Iec104Point(InfoObjectAddress: 16_386, IsSinglePoint: true, Value: 0),
        new Iec104Point(InfoObjectAddress: 16_387, IsSinglePoint: true, Value: 1),
    ]);

byte[] wire = frame.Encode();
Iec104Frame? back = Iec104Frame.TryDecode(wire);

Console.WriteLine($"sample IEC 60870-5-104 APDU · {wire.Length} bytes · {Convert.ToHexString(wire).ToLowerInvariant()}");
Console.WriteLine(back is null
    ? "decode FAILED"
    : $"decode OK · CA={back.CommonAddress} COT={back.CauseOfTransmission} points={back.Points.Count}");
Console.WriteLine("(sample telemetry, generated here; the real stack is what the token unlocks)");
return 0;
