from enum import StrEnum

class StackCommandStackNestLayerOp(StrEnum):
    NESTLAYER = "nestLayer"

    def __str__(self) -> str:
        return str(self.value)
