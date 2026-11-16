using Patina.Contracts;

namespace Patina.Library;

/// <summary>
///     A project's settings kept under a name, which a new project can start from (#144). A template is a
///     <c>project.yaml</c> with nothing in it but settings: how Auto Unwrap and Preserve Paint are set, and the export,
///     bake, viewport, checks and paint blocks. It holds no mesh, document, reference or output, since those are the
///     project's own work. Each is a file in the library's <c>project-templates</c> folder, named after it, and that
///     file is the truth: no index lists them.
/// </summary>
public static class ProjectTemplates {
    /// <summary>The folder below the library's root.</summary>
    public const string Folder = "project-templates";

    const string Extension = ".yaml";
    const int MaxNameLength = 200;

    /// <summary>The names of the templates a library keeps, sorted. A file that does not read is left out.</summary>
    public static IReadOnlyList<string> Names(string root) {
        var folder = Path.Combine(root, Folder);
        if (!Directory.Exists(folder)) {
            return [];
        }

        List<string> names = [];
        foreach (var file in Directory.EnumerateFiles(folder, "*" + Extension)) {
            try {
                names.Add(Read(file).Name);
            } catch (Exception e) when (e is LibraryFormatException or IOException or UnauthorizedAccessException) {
                // A broken or locked file is not a template anyone can start from; the rest still are.
            }
        }

        return [..names.Distinct(StringComparer.Ordinal).Order(StringComparer.CurrentCultureIgnoreCase)];
    }

    /// <summary>Keeps a project's settings as a template, replacing one of the same name.</summary>
    /// <param name="root">The library's root.</param>
    /// <param name="project">The project's path below <c>projects/</c>.</param>
    /// <param name="name">What the template is called.</param>
    /// <returns>The template's name as it was kept, trimmed.</returns>
    /// <exception cref="FileNotFoundException">The project is not there.</exception>
    /// <exception cref="ProjectRefusedException">The name cannot name a file.</exception>
    public static string Save(string root, string project, string name) {
        var kept = Named(name);
        var file = FileOf(root, kept);
        var source = ProjectFiles.Project(root, project).Document;
        var template = new ProjectDocument(ProjectId.New(), kept, [], [], []) {
            AutoUnwrap = source.AutoUnwrap,
            PreservePaint = source.PreservePaint,
            Unwrap = source.Unwrap,
            Export = source.Export,
            Bake = source.Bake,
            Viewport = source.Viewport,
            Paint = source.Paint,
            Checks = source.Checks
        };
        LibraryFiles.Replace(file, DocumentCodec.Write(template));
        return kept;
    }

    /// <summary>
    ///     Sets a project's settings to a template's, leaving its meshes, documents and name be. A block the
    ///     template does not hold, such as a bake it was never set to, leaves the project's own: a new project made
    ///     at 1024 still bakes at 1024 from a template that says nothing about baking.
    /// </summary>
    /// <exception cref="FileNotFoundException">The project or the template is not there.</exception>
    /// <exception cref="ProjectRefusedException">The name cannot name a template.</exception>
    public static void Apply(string root, string project, string name) {
        var template = Find(root, name);
        var document = ProjectFiles.Project(root, project).Document;
        LibraryFiles.WriteProject(
            root,
            project,
            document with {
                AutoUnwrap = template.AutoUnwrap,
                PreservePaint = template.PreservePaint,
                Unwrap = template.Unwrap,
                Export = template.Export ?? document.Export,
                Bake = template.Bake ?? document.Bake,
                Viewport = template.Viewport ?? document.Viewport,
                Paint = template.Paint ?? document.Paint,
                Checks = template.Checks ?? document.Checks
            }
        );
    }

    /// <summary>Throws the template away. One that is not there is not an error.</summary>
    /// <returns>Whether there was one.</returns>
    /// <exception cref="ProjectRefusedException">The name cannot name a template.</exception>
    public static bool Delete(string root, string name) {
        var file = FileOf(root, Named(name));
        if (!File.Exists(file)) {
            return false;
        }

        File.Delete(file);
        return true;
    }

    /// <summary>A template's settings, as a project document with nothing else in it.</summary>
    /// <exception cref="FileNotFoundException">There is no template of that name.</exception>
    /// <exception cref="ProjectRefusedException">The name cannot name a template.</exception>
    public static ProjectDocument Find(string root, string name) {
        var file = FileOf(root, Named(name));
        return File.Exists(file)
            ? Read(file)
            : throw new FileNotFoundException($"There is no project template '{name.Trim()}'.", name.Trim());
    }

    /// <summary>A template's name, trimmed.</summary>
    /// <exception cref="ProjectRefusedException">It is empty, too long or not text.</exception>
    static string Named(string name) {
        ArgumentNullException.ThrowIfNull(name);
        var trimmed = name.Trim();
        return trimmed.Length is 0 or > MaxNameLength || trimmed.Any(char.IsControl)
            ? throw new ProjectRefusedException($"A template's name is 1 to {MaxNameLength} characters of text.")
            : trimmed;
    }

    static ProjectDocument Read(string file) => DocumentCodec.ReadProject(SharedFile.ReadAllText(file), file).Document;

    /// <summary>Where a template of this name is kept: its name as a project's folder would spell it.</summary>
    static string FileOf(string root, string name) {
        var slug = NewProjects.Slug(name);
        if (slug.Length == 0) {
            throw new ProjectRefusedException("A template's name needs a letter or a digit, to name its file.");
        }

        NewProjects.Require(slug);
        return Path.Combine(root, Folder, slug + Extension);
    }
}
