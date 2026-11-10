using Patina.Contracts;
using Patina.Ipc;
using Patina.Jobs;
using Patina.Library;
using Patina.Server;
using Patina.Server.Api;
using Patina.Server.Graphs;
using Patina.Server.Stacks;
using Patina.Server.Viewport;
using System.Security.Cryptography;
using System.Text.Json.Serialization;

// Patina.Server: the REST API, and the owner of all domain state (plan §3.1). It starts with `--library <path>`, which
// `--create-library true` lays out first if it has to, and needs nothing else. It starts no other process, and a
// Worker connects to its socket when there is one. Listens on loopback unless given other URLs, which then need an
// access token (Api/Access.cs).

// Rooted at the program's own folder, with no configuration reload: the default roots it at the working directory and
// watches it recursively for appsettings.json changes. Patina has no appsettings.json; its settings come from the
// environment and the command line.
var builder = WebApplication.CreateSlimBuilder(
    new WebApplicationOptions {
        Args = [..args, "--hostBuilder:reloadConfigOnChange=false"], ContentRootPath = AppContext.BaseDirectory
    }
);

string? library = null;
if (builder.Configuration["library"] is { Length: > 0 } path) {
    library = InstanceRegistry.NormaliseLibrary(path);
    // A client that lays out a library needs no second program for it, and a folder that is one already is left alone.
    if (bool.TryParse(builder.Configuration["create-library"], out var create)
        && create
        && !OpenLibrary.Exists(library)) {
        LibraryFiles.CreateDefaultLibrary(library, Path.GetFileNameWithoutExtension(library));
    }

    if (!Directory.Exists(library)) {
        throw new DirectoryNotFoundException($"The library folder '{library}' does not exist.");
    }
}

// Before anything opens the library: a second Server must not reach its job queue, and nothing can be running in it.
using var taken = library is null ? null : LibraryLock.Take(library);
if (library is not null) {
    if (taken is null) {
        await Console.Error.WriteLineAsync($"Another Server has the library '{library}' open.");
        return 1;
    }

    CacheSweep.Leftovers(library);
}

// A library's Server is found through the discovery file, so it needs a token to publish and a port of its own: each
// library on the machine has one Server.
var urls = builder.Configuration["urls"] ?? (library is null ? "http://127.0.0.1:5190" : "http://127.0.0.1:0");
var token = builder.Configuration["Patina:Token"] is { Length: > 0 } configured ? configured
    : library is null ? null
    : Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
Access.RequireTokenBeyondLoopback(urls, token);
builder.WebHost.UseUrls(urls);
// The hosting log's "Request starting/finished" lines carry the query string, and a WebSocket presents the access token
// there (?access_token=, Api/Access.cs). Only its warnings are kept.
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
builder.Services.Configure<ServerOptions>(builder.Configuration.GetSection("Patina"));
builder.Services.ConfigureHttpJsonOptions(static o => {
        o.SerializerOptions.TypeInfoResolverChain.Insert(0, ContractsJsonContext.Default);
        o.SerializerOptions.TypeInfoResolverChain.Insert(1, LibraryTreeJsonContext.Default);
        o.SerializerOptions.TypeInfoResolverChain.Insert(2, ExportJsonContext.Default);
        // Numbers are numbers: the web default also accepts "1", which turns every number in the OpenAPI document
        // (and so in generated clients) into number | string.
        o.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
        // A command's "op" may come anywhere in its object: a client's JSON writer decides, not the client.
        o.SerializerOptions.AllowOutOfOrderMetadataProperties = true;
    }
);
builder.Services.AddProblemDetails();
builder.Services.AddPatinaCors();
builder.Services.AddOpenApi(static o => o.MarkNonNullablePropertiesRequired());
builder.Services.AddSingleton(new ServerIdentity(library, token));
builder.Services.AddSingleton<WorkerLink>();
builder.Services.AddHostedService(static s => s.GetRequiredService<WorkerLink>());
builder.Services.AddSingleton<LibraryHost>();
builder.Services.AddHostedService(static s => s.GetRequiredService<LibraryHost>());
builder.Services.AddSingleton<FrameMemory>();
builder.Services.AddSingleton<BakePreviewSession>();
builder.Services.AddSingleton<ViewportSession>();
builder.Services.AddHostedService(static s => s.GetRequiredService<ViewportSession>());
builder.Services.AddSingleton<GraphSessions>();
builder.Services.AddSingleton<Holders>();
builder.Services.AddSingleton<PreviewStore>();
builder.Services.AddSingleton<Thumbnails>();
builder.Services.AddSingleton<Preferences>();
builder.Services.AddSingleton<AutosaveGate>();
builder.Services.AddSingleton<GraphEvaluator>();
builder.Services.AddHostedService(static services => services.GetRequiredService<GraphEvaluator>());
builder.Services.AddSingleton<StackClips>();
builder.Services.AddSingleton<StackSessions>();
builder.Services.AddSingleton<StackEvaluator>();
builder.Services.AddHostedService(static services => services.GetRequiredService<StackEvaluator>());
builder.Services.AddSingleton(static services => new JobQueue(
        JobRunner.Folders(services.GetRequiredService<ServerIdentity>()).Jobs
    )
);
builder.Services.AddSingleton(static services => new BakeCache(
        JobRunner.Folders(services.GetRequiredService<ServerIdentity>()).Bakes
    )
);
builder.Services.AddHostedService<JobRunner>();
builder.Services.AddHostedService<Autosave>();
builder.Services.AddHostedService<DiscoveryPublisher>();

var app = builder.Build();
if (bool.TryParse(builder.Configuration["Patina:WatchStdin"], out var watchStdin) && watchStdin) {
    // Started by the desktop app: stop when it is gone, however it ended (Patina.Ipc.StdinWatch).
    StdinWatch.Start(Console.OpenStandardInput(), app.Lifetime.StopApplication);
}

// An exception no endpoint mapped answers an RFC 9457 problem (no details beyond the status), not an empty 500; a
// request Kestrel refused (a body over its limit) keeps its status.
app.UseExceptionHandler(
    new ExceptionHandlerOptions {
        StatusCodeSelector = static e => e is BadHttpRequestException bad
            ? bad.StatusCode
            : StatusCodes.Status500InternalServerError
    }
);
app.UsePatinaAccess(token);
app.UseWebSockets();
app.MapOpenApi();
app.MapPatinaApi();

app.Run();
return Environment.ExitCode;

/// <summary>Entry point, public for integration tests.</summary>
public partial class Program;
