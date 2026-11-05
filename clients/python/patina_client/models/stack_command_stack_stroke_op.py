from enum import StrEnum

class StackCommandStackStrokeOp(StrEnum):
    STROKE = "stroke"

    def __str__(self) -> str:
        return str(self.value)
