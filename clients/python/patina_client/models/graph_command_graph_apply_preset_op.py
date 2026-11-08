from enum import StrEnum

class GraphCommandGraphApplyPresetOp(StrEnum):
    APPLYPRESET = "applyPreset"

    def __str__(self) -> str:
        return str(self.value)
