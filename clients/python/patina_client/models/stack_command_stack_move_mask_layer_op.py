from enum import StrEnum

class StackCommandStackMoveMaskLayerOp(StrEnum):
    MOVEMASKLAYER = "moveMaskLayer"

    def __str__(self) -> str:
        return str(self.value)
