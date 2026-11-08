from enum import StrEnum

class GraphCommandGraphSavePresetOp(StrEnum):
    SAVEPRESET = "savePreset"

    def __str__(self) -> str:
        return str(self.value)
