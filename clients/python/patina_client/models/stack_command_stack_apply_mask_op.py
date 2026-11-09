from enum import StrEnum

class StackCommandStackApplyMaskOp(StrEnum):
    APPLYMASK = "applyMask"

    def __str__(self) -> str:
        return str(self.value)
