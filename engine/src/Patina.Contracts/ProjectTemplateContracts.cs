namespace Patina.Contracts;

/// <summary>
///     The project templates a library keeps (#144): a project's settings under a name, which a new project starts
///     from through <see cref="ProjectCreateRequest.Template" />. A template holds how Auto Unwrap and Preserve Paint
///     are set and the export, bake, viewport, checks and paint settings, never a mesh or a document.
/// </summary>
/// <param name="Names">Their names, sorted.</param>
public sealed record ProjectTemplateList(IReadOnlyList<string> Names);

/// <summary>The body of <c>POST /api/v1/library/project-templates</c>: a project's settings, kept by name.</summary>
/// <param name="Project">The project whose settings are kept, below <c>projects/</c>.</param>
/// <param name="Name">What the template is called. One of the same name is replaced.</param>
public sealed record ProjectTemplateRequest(string Project, string Name);
