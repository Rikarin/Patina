from enum import StrEnum

class GraphCommandGraphAddGroupOp(StrEnum):
    ADDGROUP = "addGroup"

    def __str__(self) -> str:
        return str(self.value)
