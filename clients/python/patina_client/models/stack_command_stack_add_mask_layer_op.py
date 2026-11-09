from enum import StrEnum

class StackCommandStackAddMaskLayerOp(StrEnum):
    ADDMASKLAYER = "addMaskLayer"

    def __str__(self) -> str:
        return str(self.value)
