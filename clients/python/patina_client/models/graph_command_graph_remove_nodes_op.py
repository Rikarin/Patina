from enum import StrEnum

class GraphCommandGraphRemoveNodesOp(StrEnum):
    REMOVENODES = "removeNodes"

    def __str__(self) -> str:
        return str(self.value)
