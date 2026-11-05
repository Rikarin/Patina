from enum import StrEnum

class StackCommandStackSetLayerOp(StrEnum):
    SETLAYER = "setLayer"

    def __str__(self) -> str:
        return str(self.value)
