from enum import StrEnum

class GraphCommandGraphMoveNodesOp(StrEnum):
    MOVENODES = "moveNodes"

    def __str__(self) -> str:
        return str(self.value)
