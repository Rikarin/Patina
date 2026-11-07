from enum import StrEnum

class StackCommandStackSetModelOp(StrEnum):
    SETMODEL = "setModel"

    def __str__(self) -> str:
        return str(self.value)
