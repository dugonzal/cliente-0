# MPLS MIB OID Standards Fix — Implementation Plan

> Goal: re-anchor the MPLS MIB OID subtree to the IANA-registered positions so a real
> NMS / net-snmp (with the standard MIBs) resolves the OIDs to real MPLS objects.
> Verified against RFC 3811 / 3812 / 3813 / 3815 / 4382 (rfc-editor).
> Ground truth (WRONG in code): the code uses `1.3.6.1.2.1.166` (mib-2) as the MPLS root.
> REAL root: `mplsStdMIB ::= { transmission 166 }` = `1.3.6.1.2.1.10.166`.

---

## Correct OID map (ground truth)

```
mplsStdMIB = 1.3.6.1.2.1.10.166

RFC 3812 MPLS-TE  (mplsTeStdMIB = .166.3)
  mplsTunnelTable  = .166.3.2.2
  mplsTunnelEntry  = .166.3.2.2.1
    col1  mplsTunnelIndex
    col2  mplsTunnelInstance
    col3  mplsTunnelIngressLSRId
    col4  mplsTunnelEgressLSRId
    col5  mplsTunnelName
    col34 mplsTunnelAdminStatus
    col35 mplsTunnelOperStatus
  INDEX { mplsTunnelIndex, mplsTunnelInstance, mplsTunnelIngressLSRId, mplsTunnelEgressLSRId }

RFC 3813 MPLS-LSR (mplsLsrStdMIB = .166.2)
  mplsLsrObjects      = .166.2.1
  mplsInSegmentTable  = .166.2.1.4   entry = .166.2.1.4.1
    col1 Index, col2 Interface, col3 Label
  mplsOutSegmentTable = .166.2.1.7   entry = .166.2.1.7.1
    col1 Index, col2 Interface, col3 PushTopLabel, col4 TopLabel, col7 NextHopAddr

RFC 3815 MPLS-LDP (mplsLdpStdMIB = .166.4)
  mplsLdpObjects = .166.4.1
  mplsLdpPeerEntry    = .166.4.1.3.2.1   (col1 mplsLdpPeerLdpId)
  mplsLdpSessionEntry = .166.4.1.3.3.1   (AUGMENTS peer)
    col1 StateLastChange, col2 SessionState, col3 SessionRole, col4 ProtocolVersion
    SessionState: 1 nonexistent, 2 initialized, 3 openrec, 4 opensent, 5 operational
    SessionRole : 1 unknown, 2 active, 3 passive

RFC 4382 MPLS-L3VPN (mplsL3VpnMIB = .166.11)
  mplsL3VpnObjects = .166.11.1
  mplsL3VpnConf    = .166.11.1.2
  mplsL3VpnVrfTable = .166.11.1.2.2  entry = .166.11.1.2.2.1
    col1 VrfName, col2 VpnId, col3 Description, col4 RD, col6 OperStatus, col7 ActiveInterfaces
  RouteTarget lives in the separate mplsL3VpnVrfRTTable (.166.11.1.2.3)
```

Vendor traffic/OAM subtree `1.3.6.1.4.1.9999.166` stays as-is (correct — RFC MIBs have no traffic/KPI columns).

---

## Tasks

### T1 — TelecomDevice.cs constants
- [ ] Change LspTableEntry, LsrInSegment, LsrOutSegment, LdpPeerEntry(l new), LdpSessionEntry, VpnVrfEntry
  - File: `src/SnmpSimulator/TelecomDevice.cs`
  - LspTableEntry  = "1.3.6.1.2.1.10.166.3.2.2.1"
  - LsrInSegment   = "1.3.6.1.2.1.10.166.2.1.4.1"
  - LsrOutSegment  = "1.3.6.1.2.1.10.166.2.1.7.1"
  - LdpPeerEntry   = "1.3.6.1.2.1.10.166.4.1.3.2.1"
  - LdpSessionEntry= "1.3.6.1.2.1.10.166.4.1.3.3.1"
  - VpnVrfEntry    = "1.3.6.1.2.1.10.166.11.1.2.2.1"

### T2 — BuildLspTable RFC columns
- [ ] Re-map columns 1..5 (index/instance/ingress/egress/name) + 34/35 (admin/oper status)
  - File: `src/SnmpSimulator/TelecomDevice.cs` BuildLspTable()
  - col5=mplsTunnelName (string), col34=mplsTunnelAdminStatus, col35=mplsTunnelOperStatus (1/2)
  - Remove the invented `mplsTunnelIsUp` (no such object in RFC 3812; map to OperStatus col35)

### T3 — BuildLsrTables RFC columns
- [ ] in-seg: col1 index, col2 interface, col3 label; drop invented OperStatus
- [ ] out-seg: col1 index, col2 interface, col3 PushTopLabel, col4 TopLabel, col7 NextHopAddr
  - File: `src/SnmpSimulator/TelecomDevice.cs` BuildLsrTables()

### T4 — BuildLdpTable on mplsLdpPeerEntry + mplsLdpSessionEntry
- [ ] Expose peer LDP id (col1) and session state/role (col2/col3), dynamic getter (operational=5 / down=1)
  - File: `src/SnmpSimulator/TelecomDevice.cs` BuildLdpTable()

### T5 — BuildVpnTable on mplsL3VpnVrfEntry
- [ ] col1 Name, col4 RD, col6 OperStatus, col7 ActiveInterfaces
  - File: `src/SnmpSimulator/TelecomDevice.cs` BuildVpnTable()

### T6 — NetworkMonitor.WalkLspsAsync
- [ ] LspTableRoot = "1.3.6.1.2.1.10.166.3.2.2"; read name from col5, oper status from col35
  - File: `src/SnmpNms/NetworkMonitor.cs`

### T7 — Tests (MplsTests.cs)
- [ ] Update Tunnel LSR id/name/admin/oper OIDs to the RFC entry
- [ ] Update LDP + VRF OIDs to RFC entry
- File: `tests/SnmpDriver.Tests/MplsTests.cs`

### T8 — Verify
- [ ] `dotnet build -c Release` → 0 warnings, 0 errors
- [ ] `dotnet test` → all green
- [ ] Real interop: start simulator, `snmpwalk` with net-snmp against mplsTunnelTable; confirm MIB resolution

### T9 — Docs
- [ ] Sync `docs/mpls-monitoring-plan.md` checkboxes (A/B already done) + correct the OIDs in
      `mpls-monitoring-layers.md` / `mpls-telemetry-and-rfc3812.md` skill references
