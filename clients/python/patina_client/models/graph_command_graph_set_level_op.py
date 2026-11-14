from enum import StrEnum

class GraphCommandGraphSetLevelOp(StrEnum):
    SETLEVEL = "setLevel"

    def __str__(self) -> str:
        return str(self.value)
