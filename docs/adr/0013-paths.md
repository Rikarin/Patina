# ADR 0013: What a path may reach

**Status:** accepted, 2026-11-06.

## What is wrong

A path reaches Patina in two ways that deserve different trust, and the Server treated them the same. A client holding the token is
the user: it can already do what the user can. A library file is not. A library is a Git repository, so a `.vxlayers` can come from
anyone, and it needs no token to reach the Server.

## Decision

1. **Names in library files stay inside.** A paint file named by a stack, and a file listed in an export's delivery record, is a name
   beside that file. One that is rooted or climbs out is refused (`LibraryLayout.Beside`), because a crafted stack could otherwise
   overwrite any file the user can write. A relative picture reference must land inside the root that found it.
2. **API paths are the caller's.** A token holder may name any file, because it is local and is the user, and pickers need that.
   Pictures chosen in the app are copied into the project, so what a project holds no longer depends on it.
3. **A remote Server is out of scope.** The API assumes a client on the same machine. Serving one further away needs library-relative
   references and a browse route first.

## Consequences

- A stack that named a paint file outside its folder no longer opens or builds.
- An absolute picture reference in a graph still reads, since a client can write one. That remains a read of a picture, not a write.
