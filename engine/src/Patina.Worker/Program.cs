using Patina.Contracts;
using Patina.Ipc;
using Patina.Viewport;
using Patina.Worker;
using System.Runtime.InteropServices;

// Patina.Worker: connects to the Server's Unix domain socket, says hello and answers what the Server asks. It holds
// nothing that cannot be rebuilt (plan §3.1), so when the link drops it just connects again.

if (args is ["--version"] or ["version"]) {
    Console.WriteLine(ProductInfo.Version);
    return 0;
}

if (args is ["--check-natives"]) {
    return NativeCheck.Run(Console.Out);
}

string? socketPath = null;
string? shaderCache = null;
var watchStdin = false;
for (var i = 0; i < args.Length; i++) {
    switch (args[i]) {
        case "--socket" when i + 1 < args.Length:
            socketPath = args[++i];
            break;
        case "--shader-cache" when i + 1 < args.Length:
            shaderCache = args[++i];
            break;
        case "--watch-stdin":
            watchStdin = true;
            break;
        default:
            socketPath = null;
            i = args.Length;
            break;
    }
}

if (socketPath is null) {
    await Console.Error.WriteLineAsync(
        "Usage: patina-worker --socket <path> [--shader-cache <folder>] [--watch-stdin] | --check-natives | --version"
    );
    return 2;
}

using var stop = new CancellationTokenSource();

void Stop(PosixSignalContext context) {
    context.Cancel = true; // leave through the end of this program, not through the runtime's default
    stop.Cancel();
}

using var interrupt = PosixSignalRegistration.Create(PosixSignal.SIGINT, Stop);
using var terminate = PosixSignalRegistration.Create(PosixSignal.SIGTERM, Stop);
if (watchStdin) {
    // Started by the desktop app: stop when it is gone, however it ended.
    StdinWatch.Start(Console.OpenStandardInput(), stop.Cancel);
}

var meshes = new MeshShelf();
using var viewport = new ViewportHost(shaderCache ?? ShaderCache.DefaultDirectory, meshes);
using var gpu = new DeviceHost();
using var graphs = new GraphHost(gpu);
var watch = new DeviceWatch();
using var stacks = new StackHost(gpu, viewport, meshes, watch);
using var bakes = new BakeHost();
using var builds = new BuildHost(gpu);
await new WorkerLoop(viewport, graphs, stacks, bakes, builds, gpu, watch).RunAsync(socketPath, Console.Out, stop.Token);
return 0;
