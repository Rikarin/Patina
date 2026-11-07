from enum import StrEnum

class GraphCommandGraphSetPortOp(StrEnum):
    SETPORT = "setPort"

    def __str__(self) -> str:
        return str(self.value)
