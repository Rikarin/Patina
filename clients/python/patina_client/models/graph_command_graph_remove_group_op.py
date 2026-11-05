from enum import StrEnum

class GraphCommandGraphRemoveGroupOp(StrEnum):
    REMOVEGROUP = "removeGroup"

    def __str__(self) -> str:
        return str(self.value)
