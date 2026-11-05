from enum import StrEnum

class StackCommandStackAddLayerOp(StrEnum):
    ADDLAYER = "addLayer"

    def __str__(self) -> str:
        return str(self.value)
