from enum import StrEnum

class GraphCommandGraphRemovePortOp(StrEnum):
    REMOVEPORT = "removePort"

    def __str__(self) -> str:
        return str(self.value)
