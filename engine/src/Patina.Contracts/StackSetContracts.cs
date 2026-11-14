namespace Patina.Contracts;

// SK7005: a wire record's constructor is its JSON shape, one parameter per field.
#pragma warning disable SK7005

/// <summary>Says how the texture set's height becomes the normal its surface is lit with.</summary>
/// <param name="Method">
///     <c>Sharp</c>, the height's slope as painted, or <c>Smooth</c>, the slope of the height blurred by a texel, which
///     hides an eight-bit height's steps.
/// </param>
public sealed record StackSetHeightNormal(string Method) : StackCommand;

/// <summary>
///     Changes the texture set's settings, any of them, as one undo step. What is left out stays as it is. Together
///     with its channels and its name these are the set's settings, and the stack file is the one place they live.
/// </summary>
/// <param name="Width">
///     The width the set is painted, evaluated and exported at, one of the sides the system offers. A canvas painted at
///     another resolution is never rewritten by this: it is sampled at the set's, and a smaller one is enlarged exactly
///     when it is next painted (#19).
/// </param>
/// <param name="Height">The height, which may differ from the width.</param>
/// <param name="UvSet">The mesh's UV set it is painted, baked and drawn in, by its number; 0 is the first.</param>
/// <param name="HeightNormal">How height becomes the normal: <c>Sharp</c> or <c>Smooth</c>.</param>
/// <param name="BakedNormal">
///     How the bake's normal reaches the set's (#86): <c>Combine</c>, beneath everything painted; <c>Replace</c>, where
///     the normal channel starts, so paint covers it; or <c>Off</c>.
/// </param>
/// <param name="BakedOcclusion">
///     How the bake's ambient occlusion reaches the set's: <c>Combine</c>, multiplied with what is painted;
///     <c>Replace</c>; or <c>Off</c>.
/// </param>
/// <param name="Description">
///     What the set is for, in a person's words (#141), which nothing evaluates or exports. Empty takes it away.
/// </param>
public sealed record StackSetSettings(
    int? Width = null,
    int? Height = null,
    int? UvSet = null,
    string? HeightNormal = null,
    string? BakedNormal = null,
    string? BakedOcclusion = null,
    string? Description = null
) : StackCommand;

/// <summary>
///     Carries every canvas of the texture set from the layout of another of the mesh's UV sets onto the one it is laid
///     out in now, as one undo step (#210): each texel takes what was painted at its place on the surface. Sent after a
///     <see cref="StackSetSettings" /> that changes the UV set, in the same batch, it keeps the paint where it was on
///     the model; on its own it projects paint laid out in another UV set onto this one. Every canvas becomes a new
///     one, so an undo gives the old ones back untouched. A surface the other layout leaves out reads as nothing.
/// </summary>
/// <param name="FromUvSet">The UV set the paint is laid out in now, by its number; 0 is the first.</param>
public sealed record StackCarryPaint(int FromUvSet) : StackCommand;

/// <summary>What Apply Channels to All Sets did to one other texture set of the model (#141).</summary>
/// <param name="Set">The set, by the material it paints.</param>
/// <param name="Path">Its stack file.</param>
/// <param name="Added">The channels it was given, by usage; none when it had them all or was left alone.</param>
/// <param name="Open">
///     Whether it is open in a session, which took the channels as an undo step of its own, unsaved. A set nobody has
///     open had them written to its file.
/// </param>
/// <param name="Problem">Why it was left as it was, or null.</param>
public sealed record StackChannelsSpread(
    string Set,
    string Path,
    IReadOnlyList<string> Added,
    bool Open,
    string? Problem = null
);
#pragma warning restore SK7005

/// <summary>What Apply Channels to All Sets did, one entry for each other texture set of the model.</summary>
/// <param name="Sets">The model's other sets, the open ones first.</param>
public sealed record StackChannelsApplied(IReadOnlyList<StackChannelsSpread> Sets);
