from enum import StrEnum

class StackCommandStackSetHeightNormalOp(StrEnum):
    SETHEIGHTNORMAL = "setHeightNormal"

    def __str__(self) -> str:
        return str(self.value)
