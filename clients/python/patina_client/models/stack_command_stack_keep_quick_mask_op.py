from enum import StrEnum

class StackCommandStackKeepQuickMaskOp(StrEnum):
    KEEPQUICKMASK = "keepQuickMask"

    def __str__(self) -> str:
        return str(self.value)
