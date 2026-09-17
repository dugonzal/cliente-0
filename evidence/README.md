# Evidence

What is here, and what a reader can and cannot check from this repository.

- `REAL-TEST-REPORT.md` — interoperability of a .NET SNMP stack against net-snmp 5.9.5 /
  5.9.5.2 on Arch Linux (.NET 10, SharpSnmpLib 12.5.7). Written 01-Sep-2026, revised
  14-Sep-2026, when the v3 authPriv row went from ❌ to ✅.

## The v3 log the report cites

`snmpv3-netsnmp-1163.txt` — the raw log behind section 2: the v3 walk (48 lines of the MPLS
subtree, no parser warnings) and the five USM failure reports, each with the counter it moves.
Reproduced 17-Sep-2026.

It is here now, and it still carries the objection that kept it out at first:

- it was produced against `SnmpSimulator`, which refuses to start without a signed license
  token, and against the closed SNMP engine (`src/SnmpUsm`);
- so nothing in it is reproducible from this repository alone. What a reader can check is that
  the log holds together — every failure moves the counter that belongs to it, and the exit
  codes say what the prose says. What they cannot do is re-run it.

## Which claims rest on it

Section 2 of the report, and only section 2: v3 authPriv SHA-256/AES-128 and SHA-512/AES-256,
the 48-line MPLS walk, and the five USM failure reports decoding to their correct `usmStats*`
OIDs. The log is in this directory; read it as a demonstration you can inspect, not as one you
can repeat.

Everything else in the report is tied to the tool named next to each figure and can be repeated
with net-snmp and the open contract build in `../open-reference/contracts/`.
