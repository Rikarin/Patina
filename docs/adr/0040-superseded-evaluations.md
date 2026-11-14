# ADR 0040: An evaluation a save waits behind is stopped between parts of its run, and nothing it began is trusted

**Status:** accepted, 2026-11-14, for issue #275.

Wave 14 made a save, a copy, a find or a read of the pixels wait as long as a Worker is connected, and put the stack it
waits on first. What it waited behind still ran to the end: an evaluation of another document, or of an older revision
of its own, already on the GPU. On a software GPU that is minutes.

## Decisions

- **Stopped between parts, never inside one.** Vixen's evaluator records a whole plan into one command list, which
  nothing can stop once it is submitted. A cancellable run (`TexturePlanEvaluator.EvaluateIncremental(…, cancellation)`)
  submits every `OpsPerCancelCheck` ops (eight), waits for the GPU and looks at its token. A run nobody can cancel stays
  one list, so a bake, an export or a test pays nothing. A drain is a bubble on a fast GPU and nothing beside one op of
  a large plan on a software one.
- **A stopped run files nothing, and nothing half done can be found again.** It throws with the frame closed and the
  device idle (a run that threw used to leave its frame open, which refused every evaluation after it). Every output it
  was repairing in place was taken out of the result cache before it began, so a half-repaired picture is never found by
  its key: it goes back to the pool as a texture with no content, and the next run makes that output whole.
  `LayerStackEvaluator` also forgets the keys it would repair from until a run finishes, because the rectangles strokes
  changed were taken for the stopped run, and a repair of only the next stroke's rectangles would miss them. Tests:
  `A_cancelled_run_files_nothing_and_the_next_draws_what_a_fresh_one_does`,
  `A_cancellable_run_in_parts_draws_what_one_list_draws` and
  `A_cancelled_evaluation_leaves_the_next_repair_whole`, byte for byte against a clean evaluation.
- **Strokes are never stopped.** A stroke is stamped into its canvas by `stack.sync` before the composite is asked for,
  and only the composite stops. The journal and the canvases stay what the Server says.
- **The Server decides, by what waits.** While an evaluation runs, the Server looks every 50 ms whether a stack that is
  waited on (`StackSession.Reading`) is behind: another stack, or a newer revision of the one being evaluated. Then it
  sends `stack.supersede` with the session and the revision, which the Worker answers at once and which stops only that
  revision's run. The answer says `superseded`, the Server publishes nothing, and its next pass evaluates the stack waited
  on first. A drag nobody waits on is never stopped, so the viewport keeps finishing pictures however fast strokes come.
- **The Knot sample ships at 1024** (Gold 1024 by 1024, Red 1024 by 512), so the first look at Patina evaluates in
  seconds on a software GPU too.

## Not yet

- Stopping inside one op. A Splatter over a 2048 map on lavapipe is one dispatch of seconds; only splitting the
  dispatch into tiles would let it stop sooner.
- An end-to-end test with two stacks. Whether a supersede lands depends on the GPU's speed, so the property is held in
  Vixen, where a cancelled token makes it certain.
