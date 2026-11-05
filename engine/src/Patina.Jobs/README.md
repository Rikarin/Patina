The job queue the Server owns (plan §3.1). Each job is one JSON file, replaced atomically, so a killed Server loses at most the
last progress report. On start, a job that was running goes back in the queue: the Worker that had it is gone or will be told
nothing, and a bake holds nothing that cannot be run again. `BakeCache` holds what jobs made, a folder per build key. Batch rebuilds
and impact analysis arrive here in P6. `BuildPlan` works out what a build would do from build keys, writing nothing, so a preview and a resumed build are the same act (ADR 0011).
