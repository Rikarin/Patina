from enum import StrEnum

class StackCommandStackCarryPaintOp(StrEnum):
    CARRYPAINT = "carryPaint"

    def __str__(self) -> str:
        return str(self.value)
