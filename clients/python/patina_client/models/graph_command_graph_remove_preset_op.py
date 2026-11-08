from enum import StrEnum

class GraphCommandGraphRemovePresetOp(StrEnum):
    REMOVEPRESET = "removePreset"

    def __str__(self) -> str:
        return str(self.value)
