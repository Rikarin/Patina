from enum import StrEnum

class GraphEventKind(StrEnum):
    CLOSED = "closed"
    EDITED = "edited"
    EVALUATED = "evaluated"
    REDONE = "redone"
    RESYNC = "resync"
    SAVED = "saved"
    SETTLED = "settled"
    TYPESCHANGED = "typesChanged"
    UNDONE = "undone"

    def __str__(self) -> str:
        return str(self.value)
