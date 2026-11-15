using Patina.Contracts;

namespace Patina.Ipc;

/// <summary>One attempt at a job: a report from an earlier attempt is one that arrived too late.</summary>
/// <param name="Job">The job.</param>
/// <param name="Attempt">Which attempt.</param>
public sealed record BakeTicket(string Job, int Attempt);

/// <summary>
///     A bake for the Worker to run. It is a copy: the Server has the job, and sends it again after a crash.
/// </summary>
/// <param name="Ticket">Which attempt this is.</param>
/// <param name="Request">What to bake.</param>
/// <param name="Folder">The folder to make, which is the cache's for this key.</param>
public sealed record BakeStart(BakeTicket Ticket, BakeRequest Request, string Folder);

/// <summary>How far a bake is.</summary>
public sealed record BakeProgressNote(BakeTicket Ticket, double Fraction);

/// <summary>How a bake ended. A cancelled bake has neither a manifest nor an error.</summary>
/// <param name="Ticket">Which attempt.</param>
/// <param name="Manifest">What is in the folder, when it finished.</param>
/// <param name="Error">Why it could not be done, when it could not.</param>
public sealed record BakeEnding(BakeTicket Ticket, BakeManifest? Manifest, string? Error);

/// <summary>One step of a build. It is a copy: the Server holds the job, and plans it again after a crash.</summary>
/// <param name="Ticket">Which attempt this is.</param>
/// <param name="Output">An output to build, or null.</param>
/// <param name="Export">A preset to run, or null.</param>
public sealed record BuildStart(BakeTicket Ticket, OutputStep? Output, ExportStep? Export);

/// <summary>How a step ended. A cancelled step has no manifest and no error.</summary>
/// <param name="Ticket">Which attempt.</param>
/// <param name="Output">What an output made.</param>
/// <param name="Export">What a preset made.</param>
/// <param name="Error">Why it could not be done.</param>
public sealed record BuildEnding(BakeTicket Ticket, BuildManifest? Output, ExportManifest? Export, string? Error);

/// <summary>How far a step of a build is, and what it is on.</summary>
/// <param name="Ticket">Which attempt.</param>
/// <param name="Fraction">How far through the step, from 0 to 1.</param>
/// <param name="File">What it is making now, for a person: a preset's file, with its texture set.</param>
public sealed record BuildProgressNote(BakeTicket Ticket, double Fraction, string File);
