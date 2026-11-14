using Patina.Ipc;
using Vixen.Authoring;
using Vixen.Texturing.Layers;
using Vixen.Texturing.Painting;

namespace Patina.Worker;

sealed partial class StackHost {
    internal sealed partial class Session {
        /// <summary>
        ///     A canvas made a copy of another, in place. ⚠ Into the canvas object that is there and never a new one: a
        ///     replay begins every stroke before it stamps any, so a stroke on the copy is bound to its pixels before
        ///     the copy is made. Done again, the source is as it was the first time, since the journal undid what came
        ///     after.
        /// </summary>
        sealed class CanvasCopy(PaintCanvasStore canvases, string target, string source, int width, int height)
            : IAuthoringCommand {
            public string Name => "Copy";

            /// <summary>
            ///     The layouts a copy is carried between (#210): what it copies is laid out in the first, and it in the
            ///     second. Null for a copy laid out as what it copies.
            /// </summary>
            public (PaintProjection From, PaintProjection To)? Carry { get; init; }

            public void Do(ICommandContext context) {
                var into = Target();
                var from = canvases.Open(source);

                // A copy from a stack of another size, or from a canvas painted before this set's size changed, is made
                // this set's size (#106): larger exactly, smaller by an average. The source is left as it was.
                if (from is not null && (from.Width != into.Width || from.Height != into.Height)) {
                    from = from.Resampled(into.Width, into.Height);
                }

                if (from is not null && Carry is { } layouts) {
                    from = PaintTransfer.Move(from, layouts.From, layouts.To);
                }

                foreach (var usage in into.Channels.Where(usage => from?.Has(usage) != true)) {
                    into.Channel(usage).Fill(0u);
                }

                foreach (var usage in from?.Channels ?? []) {
                    into.Channel(usage).CopyFrom(from!.Channel(usage));
                }
            }

            /// <summary>
            ///     Back to what a new canvas holds, which nothing reads once the layer naming it has gone.
            /// </summary>
            public void Undo(ICommandContext context) {
                var into = Target();
                foreach (var usage in into.Channels) {
                    into.Channel(usage).Fill(0u);
                }
            }

            PaintCanvas Target() {
                if (canvases.Open(target) is { } held) {
                    return held;
                }

                PaintCanvas made = new(width, height);
                canvases.Adopt(target, made);
                return made;
            }

            public bool TryMergeWith(
                IAuthoringCommand previous,
                [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IAuthoringCommand? merged
            ) {
                merged = null;
                return false;
            }
        }

        /// <summary>
        ///     A canvas made what one group of a document composites, which is what a flattened group shows. ⚠ Into
        ///     the canvas object that is there, for <see cref="CanvasCopy" />'s reason, and from the document the
        ///     journal holds rather than the one standing now: a replay after a lost Worker writes what the artist saw.
        /// </summary>
        sealed class CanvasCapture(Session session, string target, StackCapture capture) : IAuthoringCommand {
            public string Name => "Flatten";

            public void Do(ICommandContext context) {
                var document = LayerStackYaml.Read(capture.Document);
                var into = Target(document.BaseWidth, document.BaseHeight);
                foreach (var usage in into.Channels) {
                    into.Channel(usage).Fill(0u);
                }

                if (LayerStackEdit.SetFor(document, session.set) is not { } set) {
                    return;
                }

                // A draft, as the picture the artist was looking at is one; a refusal leaves the canvas empty, which
                // the stack's own problems will already be saying.
                using LayerStackEvaluator run = new(session.evaluator, session.canvases, session.path, session.fill) {
                    Draft = true,
                    Capture = capture.Layer,
                    Backdrop = capture.Backdrop,
                    PositionBox = session.positionBox
                };
                if (run.Prepare(document, set, session.compounds.Registry, session.compounds.Source).Count > 0) {
                    return;
                }

                using var bake = run.Evaluate();
                foreach (var output in run.Outputs) {
                    var map = bake.Read(output.Image);
                    if (map.Width == into.Width && map.Height == into.Height) {
                        into.Channel(output.Usage).CopyFrom(map.Pixels);
                    }
                }
            }

            /// <summary>Back to what a new canvas holds, which nothing reads once the group is not flattened.</summary>
            public void Undo(ICommandContext context) {
                if (session.canvases.Open(target) is { } held) {
                    foreach (var usage in held.Channels) {
                        held.Channel(usage).Fill(0u);
                    }
                }
            }

            PaintCanvas Target(int width, int height) {
                if (session.Made(target, (width, height)) is { } held) {
                    return held;
                }

                PaintCanvas made = new(width, height);
                session.canvases.Adopt(target, made);
                return made;
            }

            public bool TryMergeWith(
                IAuthoringCommand previous,
                [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IAuthoringCommand? merged
            ) {
                merged = null;
                return false;
            }
        }

        /// <summary>A stroke that stamped nothing, which still stands in the journal.</summary>
        sealed class Nothing : IAuthoringCommand {
            public string Name => "Stroke";

            public void Do(ICommandContext context) { }

            public void Undo(ICommandContext context) { }

            public bool TryMergeWith(
                IAuthoringCommand previous,
                [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IAuthoringCommand? merged
            ) {
                merged = null;
                return false;
            }
        }
    }
}
