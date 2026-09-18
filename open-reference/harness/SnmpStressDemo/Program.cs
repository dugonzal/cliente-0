using System.Diagnostics;
using Lextm.SharpSnmpLib;
using SnmpDriver;
using SnmpContracts.Models;
using SnmpSimulator;

// SnmpStressDemo — pushes a large number of parallel SNMP polls through one shared,
// multiplexed UDP transport against several simulated agents, and reports throughput.
//   --count <n>     total poll jobs (default 50000)
//   --agents <k>    simulated agents to poll (default 3)
//   --parallel <n>  max concurrent in flight (default 512)

Dictionary<string, string?> parsed = ParseArgs(args);
int count = int.TryParse(Get(parsed, "--count"), out int c) ? c : 50_000;
int agentsCount = int.TryParse(Get(parsed, "--agents"), out int a) ? a : 3;
int parallel = int.TryParse(Get(parsed, "--parallel"), out int p) ? p : 512;

using CancellationTokenSource cts = new CancellationTokenSource();
List<SnmpAgentHost> agents = new List<SnmpAgentHost>();
for (int i = 0; i < agentsCount; i++)
{
    SnmpAgentHost agent = new SnmpAgentHost(DeviceProfileLoader.CreateExample($"Agent-{i}"), 0, "public", "private");
    agents.Add(agent);
    _ = agent.RunAsync(cts.Token);
}

// Discover the full set of OIDs on the first agent (shared by all simulated devices).
string host = "127.0.0.1";
using SnmpClient seed = new SnmpClient(SnmpConfig.CommunityConfig(host, "public", SnmpVersion.V2c, agents[0].LocalPort));
List<ObjectIdentifier> oids = new List<ObjectIdentifier>();
oids.AddRange(seed.Walk(new ObjectIdentifier("1.3.6.1.2.1.1")).Select(v => v.Oid));
oids.AddRange(seed.Walk(new ObjectIdentifier("1.3.6.1.2.1.2.2.1")).Select(v => v.Oid));
oids.AddRange(seed.Walk(new ObjectIdentifier("1.3.6.1.4.1.9999.9.1")).Select(v => v.Oid));

List<PollTarget> targets = new List<PollTarget>(count);
for (int i = 0; i < count; i++)
{
    SnmpAgentHost agent = agents[i % agents.Count];
    ObjectIdentifier oid = oids[i % oids.Count];
    targets.Add(new PollTarget(SnmpConfig.CommunityConfig(host, "public", SnmpVersion.V2c, agent.LocalPort), oid));
}

Console.WriteLine($"Pooling {count:N0} SNMP polls -> {agentsCount} agents over 1 shared socket (max {parallel:N0} in flight)");
Stopwatch sw = Stopwatch.StartNew();

int ok, fail;
using (SnmpPoller poller = new SnmpPoller())
{
    IReadOnlyList<PollOutcome> results = await poller.PollAsync(targets, parallel, cts.Token);
    ok = results.Count(r => r.Ok);
    fail = results.Count(r => !r.Ok);
}

sw.Stop();
double rate = count / sw.Elapsed.TotalSeconds;
Console.WriteLine($"Done in {sw.Elapsed.TotalSeconds:F2}s: {ok:N0} ok, {fail:N0} failed => {rate:N0} responses/s");
Console.WriteLine($"=> throughput: {count / sw.Elapsed.TotalSeconds:F0} req/s (single UDP socket, {agentsCount} agents)");

foreach (SnmpAgentHost agent in agents) agent.Dispose();

static string? Get(Dictionary<string, string?> m, string k) => m.TryGetValue(k, out string? v) ? v : null;
static Dictionary<string, string?> ParseArgs(string[] raw)
{
    Dictionary<string, string?> map = new Dictionary<string, string?>(StringComparer.Ordinal);
    for (int i = 0; i < raw.Length; i++)
        if (raw[i].StartsWith("--") && i + 1 < raw.Length)
            map[raw[i]] = raw[i + 1];
    return map;
}
