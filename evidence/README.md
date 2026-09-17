# Evidence

What is here, and what a reader can and cannot check from this repository.

- `REAL-TEST-REPORT.md` — interoperability of a .NET SNMP stack against net-snmp 5.9.5 /
  5.9.5.2 on Arch Linux (.NET 10, SharpSnmpLib 12.5.7). Written 01-Sep-2026, revised
  14-Sep-2026, when the v3 authPriv row went from ❌ to ✅.

## One artifact the report cites is not here

`linkedin-evidence/snmpv3-netsnmp-1163.txt` — the raw log of the v3 walk (48 lines of the MPLS
subtree, no parser warnings) and the raw output of the five USM failure reports. It is absent
on purpose, not by accident:

- it was produced against `SnmpSimulator`, which refuses to start without a signed license
  token, and against the closed SNMP engine (`src/SnmpUsm`);
- so nothing in that log is reproducible from this repository alone, and publishing the log
  would be publishing the engine's behaviour without the engine.

## Which claims rest on it

Section 2 of the report, and only section 2: v3 authPriv SHA-256/AES-128 and SHA-512/AES-256,
the 48-line MPLS walk, and the five USM failure reports decoding to their correct `usmStats*`
OIDs. Read those as a demonstration that can be shown on request, not as an artifact you hold.

Everything else in the report is tied to the tool named next to each figure and can be repeated
with net-snmp and the open contract build in `../open-reference/contracts/`.
