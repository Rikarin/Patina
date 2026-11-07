from enum import StrEnum

class StackCommandStackRemoveLayerOp(StrEnum):
    REMOVELAYER = "removeLayer"

    def __str__(self) -> str:
        return str(self.value)
