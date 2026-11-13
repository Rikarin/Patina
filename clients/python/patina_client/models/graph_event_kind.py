from enum import StrEnum

class GraphEventKind(StrEnum):
    AUTOSAVED = "autosaved"
    AUTOSAVEFAILED = "autosaveFailed"
    CLOSED = "closed"
    EDITED = "edited"
    EVALUATED = "evaluated"
    PACED = "paced"
    REDONE = "redone"
    RESYNC = "resync"
    SAVED = "saved"
    SELECTED = "selected"
    SETTLED = "settled"
    TYPESCHANGED = "typesChanged"
    UNDONE = "undone"

    def __str__(self) -> str:
        return str(self.value)
