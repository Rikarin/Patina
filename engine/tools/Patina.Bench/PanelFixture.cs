// SPDX-License-Identifier: GPL-3.0-or-later

using Vixen.Core.Mathematics;
using Vixen.Geometry;
using Vixen.Interchange;

namespace Patina.Bench;

/// <summary>
///     A high and low pair whose parts meet inside one chart, for Shut Lines (#118) and for an artist to judge it: a
///     plate cut into panels. The low is one quad with one chart, so nothing about its atlas has a border inside the
///     plate. The high is the same plate as separate panels with narrow gaps between them, so an id bake draws the
///     panels as different parts across the one chart, and every line where two of them meet is a shut line.
/// </summary>
/// <remarks>
///     The housing has one body and a strap, and the only border its id map has runs along the strap, where the
///     strap's own chart ends; Shut Lines keeps off chart borders on purpose, so the housing showed nothing. Here the
///     borders are where a panel ends and the chart goes on.
/// </remarks>
public static class PanelFixture {
    public const string HighFile = "panels_high.glb";
    public const string LowFile = "panels_low.glb";

    /// <summary>The panels across and down.</summary>
    public const int Columns = 3;

    public const int Rows = 2;

    /// <summary>Half the plate's side.</summary>
    const float Half = 1f;

    /// <summary>The gap between two panels, as a share of the plate's side.</summary>
    const float Gap = 0.015f;

    /// <summary>How far the low sits above the high, so its rays leave clear of the panels.</summary>
    const float Lift = 0.01f;

    /// <summary>The chart's margin inside the atlas.</summary>
    public const float Margin = 0.03f;

    /// <summary>Writes both files into a folder.</summary>
    /// <param name="folder">Where they go.</param>
    /// <returns>Nought.</returns>
    public static int Write(string folder) {
        Directory.CreateDirectory(folder);

        List<MeshData> panels = [];
        var width = 2f * Half / Columns;
        var depth = 2f * Half / Rows;

        for (var row = 0; row < Rows; row++) {
            for (var column = 0; column < Columns; column++) {
                var x0 = -Half + column * width + Gap;
                var z0 = -Half + row * depth + Gap;
                panels.Add(Plate($"panel_{row}_{column}", x0, z0, x0 + width - 2f * Gap, z0 + depth - 2f * Gap, 0f));
            }
        }

        ModelWriter.Write(Path.Combine(folder, HighFile), panels, "panels");
        ModelWriter.Write(Path.Combine(folder, LowFile), [Plate("plate", -Half, -Half, Half, Half, Lift)], "panels");

        Console.WriteLine($"{Path.Combine(folder, HighFile)}: {panels.Count} panels");
        Console.WriteLine($"{Path.Combine(folder, LowFile)}: one chart");

        return 0;
    }

    /// <summary>One flat quad facing up, mapped onto the whole atlas inside its margin.</summary>
    static MeshData Plate(string name, float x0, float z0, float x1, float z1, float height) {
        Vector3[] positions = [new(x0, height, z0), new(x0, height, z1), new(x1, height, z1), new(x1, height, z0)];
        var up = Vector3.UnitY;

        static float Across(float value, float from, float to) =>
            Margin + (value - from) / (to - from) * (1f - 2f * Margin);

        // ⚠ v runs down the picture, so the far edge (z1) is at the top of the chart, as BakeFixture's charts are.
        Vector2[] coordinates = [
            ..positions.Select(point => new Vector2(
                    Across(point.X, -Half, Half),
                    1f - Across(point.Z, -Half, Half)
                )
            )
        ];

        return new() {
            Name = name,
            Positions = [..positions],
            Normals = [up, up, up, up],
            TexCoords = [..coordinates],
            Indices = [0, 1, 2, 0, 2, 3],
            Bounds = new(positions.Aggregate(Vector3.Min), positions.Aggregate(Vector3.Max))
        };
    }
}
