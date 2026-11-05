from enum import StrEnum

class GraphCommandGraphAddNodeOp(StrEnum):
    ADDNODE = "addNode"

    def __str__(self) -> str:
        return str(self.value)
