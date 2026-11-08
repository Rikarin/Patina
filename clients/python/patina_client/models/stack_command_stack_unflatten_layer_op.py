from enum import StrEnum

class StackCommandStackUnflattenLayerOp(StrEnum):
    UNFLATTENLAYER = "unflattenLayer"

    def __str__(self) -> str:
        return str(self.value)
