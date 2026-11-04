# Engine

The .NET side of Patina: the Server (owns all state), the Worker (owns the GPU, holds nothing) and the CLI.
`dotnet build Patina.slnx` and `dotnet test --solution Patina.slnx` run everything. To run the two processes by hand:

```sh
dotnet run --project src/Patina.Server -- --library /path/to/Game.ptlib --Patina:Worker:SocketPath=/tmp/patina.sock
dotnet run --project src/Patina.Worker -- --socket /tmp/patina.sock
```

After an API change, review the difference and update the committed document with
`PATINA_UPDATE_OPENAPI=1 dotnet test --solution Patina.slnx`, then `pnpm exec nx run api-client:generate` in `editor/`.
