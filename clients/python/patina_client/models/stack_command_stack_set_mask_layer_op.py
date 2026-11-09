from enum import StrEnum

class StackCommandStackSetMaskLayerOp(StrEnum):
    SETMASKLAYER = "setMaskLayer"

    def __str__(self) -> str:
        return str(self.value)
