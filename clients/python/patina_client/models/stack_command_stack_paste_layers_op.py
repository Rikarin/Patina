from enum import StrEnum

class StackCommandStackPasteLayersOp(StrEnum):
    PASTELAYERS = "pasteLayers"

    def __str__(self) -> str:
        return str(self.value)
