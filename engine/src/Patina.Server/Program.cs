using Patina.Contracts;
using Patina.Ipc;
using Patina.Library;
using Patina.Server;
using Patina.Server.Api;
using System.Security.Cryptography;
using System.Text.Json.Serialization;

// Patina.Server: the REST API, and the owner of all domain state (plan §3.1). It starts with `--library <path>` and
// needs nothing else, it starts no other process, and a Worker connects to its socket when there is one. Listens on
// loopback unless given other URLs, which then need an access token (Api/Access.cs).

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
    if (!Directory.Exists(library)) {
        throw new DirectoryNotFoundException($"The library folder '{library}' does not exist.");
    }
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
        // Numbers are numbers: the web default also accepts "1", which turns every number in the OpenAPI document
        // (and so in generated clients) into number | string.
        o.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
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
app.MapOpenApi();
app.MapPatinaApi();

app.Run();

/// <summary>Entry point, public for integration tests.</summary>
public partial class Program;
