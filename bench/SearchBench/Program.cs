using SearchBench;

if (args.Length == 0 || args[0] is not ("build" or "throughput"))
{
    Console.Error.WriteLine("usage: SearchBench build|throughput [--server URL] [--threads 1,2] [--modes striped,global] [--runs N]");
    Console.Error.WriteLine("       SearchBench throughput [--clients 1,2] [--duration S] [--expand true|false]");
    return 1;
}

var options = new Dictionary<string, string>();
for (var i = 1; i + 1 < args.Length; i += 2)
{
    options[args[i].TrimStart('-')] = args[i + 1];
}

string Option(string name, string fallback) => options.GetValueOrDefault(name, fallback);
int[] Ints(string text) => text.Split(',').Select(int.Parse).ToArray();

var server = new Uri(Option("server", BenchConstants.DefaultServer));
if (args[0] == "build")
{
    await BuildSweep.RunAsync(
        server,
        Ints(Option("threads", BenchConstants.DefaultBuildThreads)),
        Option("modes", BenchConstants.DefaultBuildModes).Split(','),
        int.Parse(Option("runs", BenchConstants.DefaultBuildRuns.ToString())));
}
else
{
    await ThroughputRun.RunAsync(
        server,
        Ints(Option("clients", BenchConstants.DefaultClients)),
        int.Parse(Option("duration", BenchConstants.DefaultDurationSeconds.ToString())),
        bool.Parse(Option("expand", "false")));
}

return 0;
