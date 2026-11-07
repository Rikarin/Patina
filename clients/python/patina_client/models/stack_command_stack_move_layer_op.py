from enum import StrEnum

class StackCommandStackMoveLayerOp(StrEnum):
    MOVELAYER = "moveLayer"

    def __str__(self) -> str:
        return str(self.value)
