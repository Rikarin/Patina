from enum import StrEnum

class GraphCommandGraphSetGroupOp(StrEnum):
    SETGROUP = "setGroup"

    def __str__(self) -> str:
        return str(self.value)
