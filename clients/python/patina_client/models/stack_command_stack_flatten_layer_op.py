from enum import StrEnum

class StackCommandStackFlattenLayerOp(StrEnum):
    FLATTENLAYER = "flattenLayer"

    def __str__(self) -> str:
        return str(self.value)
