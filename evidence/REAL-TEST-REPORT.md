# Real-world interop test report (net-snmp 5.9.5)

These tests exercised the driver and simulator against a **real** SNMP agent/client
(net-snmp 5.9.5, the standard implementation used by real telecom/network equipment),
instead of only the same-library loopback setup.

Environment: Arch Linux, net-snmp 5.9.5, .NET 10, SharpSnmpLib 12.5.7.

## 1. Driver ↔ real net-snmp `snmpd` (the driver polls a real agent)

Started `snmpd -c /tmp/snmpd-test.conf` on 127.0.0.1:1162. Our driver (`SnmpPollDemo`)
polled it over v1 and v2c:

```
sysDescr  : Linux ciclo 7.1.10-arch1-1 #1 SMP PREEMPT_DYNAMIC ... x86_64   (real kernel)
sysUpTime : 00:00:26.08                                                    (real uptime)
Interface table (ifTable) walk:  lo, MEDIATEK MT7921K Wi-Fi 6E, virbr0,
                                  wg0, br-*, docker0, veth*  → REAL host interfaces
```

The driver reads the real agent's system group and walks the real interface table.
(Custom vendor-health OIDs correctly surface as `noSuchObject` — the real agent does not expose them.)

## 2. Real net-snmp client ↔ simulator (does the simulator speak standard SNMP?)

Started our `SnmpSimulator.Cli` (OLT) on 127.0.0.1:1163. Tested with real tools:

| Tool / version | Result |
|---|---|
| `snmpget -v2c` system + vendor health | ✅ sysDescr, cpu=35 |
| `snmpwalk -v2c` system group | ✅ all 7 leaves, uptime 210 days |
| `snmpwalk -v2c` ifTable | ✅ PON0/1, PON0/2, Uplink0/0, ethernetCsmacd |
| `snmpbulkget -v2c` | ✅ bulk works |
| `snmpset -v2c` sysName | ✅ wrote and returned `SetByRealTool` |
| `snmpget -v3 -l authNoPriv` (SHA-256) | ✅ returned cpu=35 |
| `snmpget -v3 -l authPriv` (SHA-256/AES-128) | ✅ (superseded 14-Sep-2026; was ❌ on 01-Sep — see §3) |
| `snmpget -v3 -l authPriv` (SHA-512/AES-256) | ✅ (14-Sep-2026, Blumenthal key extension) |

## 3. Findings / known interop gaps

- **v1, v2c: full interoperability** both directions (driver↔snmpd, snmpd-tools↔simulator).
- **v3 authNoPriv: interoperates** with the real net-snmp client (SHA-256 auth verified).
- ~~**v3 authPriv (AES-128): does not interoperate with net-snmp** (key-derivation mismatch,
  could not fix).~~ **SUPERSEDED 2026-09-14:** fixed on 2026-09-11 in
  `src/SnmpUsm/AesPrivacyProvider.cs` by using the **Blumenthal** localized-key extension
  (net-snmp's rule) instead of SharpSnmpLib's **Reeder** variant. Re-verified against
  net-snmp 5.9.5.2: `authPriv` SHA-256/AES-128 and SHA-512/AES-256 both OK, full MPLS
  subtree walk over v3 (48 lines, no parser warnings), and all five USM failure reports
  (unknown user / wrong digest / unsupported secLevel / decryption / notInTimeWindow)
  decode to their correct `usmStats*` OIDs. Note: usmStats counters ride in the report
  PDU, they are not GET-able from the tree. Evidence: `linkedin-evidence/snmpv3-netsnmp-1163.txt`.

## 4. Performance check (driver hot path)

Optimization (remove per-request `Task.Delay` timer + LINQ in the walk loop):

| Batch | Before | After |
|---|---|---|
| 30k polls / 3 agents | 18,010 req/s | 20,556 req/s |
| 60k polls / 3 agents | — | 21,539 req/s |
| 120k polls / 6 agents | — | 24,609 req/s, 0 failures |

The driver is non-blocking over one multiplexed socket; the loopback agent+UDP is the
practical ceiling. The design has no per-request socket or timer allocation.

### Re-measured 2026-09-14 (same compiled driver, harness outside the repo)

| total req | agents | placement | in-flight | req/s | failures |
|---|---|---|---|---|---|
| 120,000 | 6 | separate processes | 256 | 38,302 | 0 |
| 120,000 | 6 | separate processes | 512 | 38,792 | 0 |
| 120,000 | 6 | same process | 256 | 36,109 | 0 |
| 120,000 | 6 | same process | 512 | 30,252 | 0 |
| 30,000 | 1 | separate processes | 256 | **39,746** | 0 |

The 01-Sep numbers do not reproduce — the driver is ~55% faster today. Agent count is NOT
the scaling variable here: one loopback agent matches six (~36–40k req/s is the client +
loopback ceiling), and oversubscribing in-flight requests in-process costs ~16%.
