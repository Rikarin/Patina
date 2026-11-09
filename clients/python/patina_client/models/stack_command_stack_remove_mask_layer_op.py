from enum import StrEnum

class StackCommandStackRemoveMaskLayerOp(StrEnum):
    REMOVEMASKLAYER = "removeMaskLayer"

    def __str__(self) -> str:
        return str(self.value)
