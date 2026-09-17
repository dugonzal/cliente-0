# MPLS Monitoring System — Implementation Plan

> Granular plan (2-5 min per task) following the writing-plans format.
> Every task is anchored to an IETF/ITU-T RFC so it is industry-credible.
> Done so far: RFC 3812 (mplsTunnelTable), RFC 3813 (LFIB in/out segments),
> traffic engineering (rate/capacity/octets), OAM KPIs (latency/jitter/loss), congestion alarm,
> RFC 3815 (LDP sessions), RFC 4382 (L3VPN VRFs), RFC 8029 (OAM SLA violation alarm),
> RFC 4090 (FRR fast-reroute with backup path + MIB columns), multi-hop P/PE/CE topology with
> Degraded status, and the operator board `/api/mpls` MPLS panel.

> ✅ **MPLS MIB OID standards fix (verified 2026-09-07)** — the MPLS MIB OID subtree is
> re-anchored to the IANA registration `mplsStdMIB ::= { transmission 166 }` = `1.3.6.1.2.1.10.166`
> (was wrongly `1.3.6.1.2.1.166`). Columns re-mapped to the RFC positions, indexes fixed,
> and verified with net-snmp: all four MPL MIBs (RFC 3812/3813/3815/4382) now resolve
> symbolically with correct types. See `docs/mpls-oid-standards-fix.md`.

---

## Phase A — Signaling: MPLS-LDP (RFC 3815)

- [x] **A1. Add LdpState model to TelecomDevice**
- [x] **A2. Build RFC 3815 MIB (mplsLdpSessionTable)** — now on `mplsLdpPeerEntry`/`mplsLdpSessionEntry`
- [x] **A3. LDP command to flap a session**
- [x] **A4. LDP test (MplsTests.cs)**

## Phase B — Service: MPLS L3VPN (RFC 4364 / RFC 4382)

- [x] **B1. Add VrfState model**
- [x] **B2. Build RFC 4382 MIB (mplsL3VpnVrfTable)** — index by VRF name
- [x] **B3. VRF command**
- [x] **B4. L3VPN test**

---

## Phase C — OAM: MPLS Ping / LSP Probe (RFC 8029)

- [ ] **C1. Proactive LSP probe**
  - File: `src/SnmpNms/NetworkMonitor.cs`
  - In `WalkLspsAsync` (already reads rate/capacity), also compute reachability via the OAM latency jitter getter already present.
  - Decide: no new SNMP needed — reuse the OAM KPIs (latency/jitter/loss) for SLA evaluation instead of a separate ping.

- [X] **C2. SLA violation alarm**
  - File: `src/SnmpNms/Fault/FaultManager.cs`
  - Add `EvaluateMplsSla(LspHealth lsp, string device, double maxLatencyMs, double maxLossPct)`:
    - Raise `LspSlaViolated` (Major) if latency > threshold or loss > threshold.
    - Clear when back under.
  - Call from `WalkLspsAsync`.

- [X] **C3. OAM SLA test**
  - File: `tests/SnmpDriver.Tests/MplsTests.cs`
  - `[Fact] Lsp_sla_violation_is_raised_when_latency_high`: LspHealth(latency 200) → alarm; healthy → clear.

---

## Phase D — Protection: FRR (Fast Reroute)

- [X] **D1. Add frr state**
  - File: `src/SnmpSimulator/TelecomDevice.cs`
  - Add `bool FrrActive` to `LspState` + a backup path label/nbr.
  - `SeedLsps()`: mark tunnel 1 with `FrrActive=false`, backup next-hop `P9`.

- [X] **D2. FRR command**
  - File: `src/SnmpSimulator/TelecomDevice.cs`
  - `show mpls lsp frr` → `lsp 1: primary=P1 backup=P9 protection=ready|active`.
  - `tunnel 1 primary shutdown` → flips `FrrActive=true` (rerouted to backup).

- [X] **D3. FRR reflected in MIB**
  - File: `src/SnmpSimulator/TelecomDevice.cs`
  - Expose `mplsTunnelFrrActive` in vendor subtree (col 8) → 0/1.
  - Expose `mplsTunnelBackupNbr` (col 9) as OctetString.

- [X] **D4. FRR test**
  - File: `tests/SnmpDriver.Tests/MplsTests.cs`
  - `[Fact] Frr_reroutes_lsp_to_backup_when_primary_fails`: check status, execute primary shutdown, verify frr column = 1, restore.

---

## Phase E — Network-wide topology (P/PE/CE)

- [X] **E1. Multi-hop LSP path**
  - File: `src/SnmpNms/Topology/Topology.cs`
  - Extend `CircuitKind` won't change; instead add `MplsLsp` path modelling via `TopologyManager`:
    - Add `DeclarePath(string lspId, string headend, string[] path)` storing hops.
  - Test: single `MplsLsp` circuit already correlates CircuitDown when headend tunnel down.

- [X] **E2. Degraded via protection**
  - File: `tests/SnmpDriver.Tests/MplsTests.cs` (or TopologyTests)
  - When FRR active, topology stays `Degraded` (redundancy used, connectivity intact) rather than `Partitioned`.

---

## Phase F — MPLS-specific dashboard API

- [X] **F1. Add /api/mpls to NmsDashboard**
  - File: `examples/NmsDashboard/NmsDashboardHost.cs`
  - New route returning per-LSP JSON: name, admin/oper, rate, capacity, utilisation, latency, jitter, loss, frr, vrfs.
  - Read the existing `/api/summary` for pattern.

- [X] **F2. Dashboard panel**
  - File: `examples/NmsDashboard/` (HTML served by host)
  - Add an "MPLS" panel listing LSPs with utilisation % and SLA state.

---

## Verification (after each phase)
- `dotnet build` → 0 warnings, 0 errors.
- `dotnet test --filter "FullyQualifiedName~MplsTests"` → all pass.
- Commit in English after each phase: `feat(mpls): add RFC 3815 LDP sessions`, etc.

## Standards map
| Feature | RFC/ITU-T |
|---|---|
| LSP tunnel state | RFC 3812 (MPLS-TE MIB) |
| Label forwarding (LFIB) | RFC 3813 (MPLS-LSR MIB) |
| Signalling | RFC 3815 (MPLS-LDP MIB) |
| L3VPN service | RFC 4364 / RFC 4382 |
| OAM (ping/probe) | RFC 8029 |
| Protection (FRR) | RFC 4090 |
| Carrier Ethernet | ITU-T G.8032 / MPLS-TP (RFC 5921) |
