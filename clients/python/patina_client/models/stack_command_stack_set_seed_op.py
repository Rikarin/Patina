from enum import StrEnum

class StackCommandStackSetSeedOp(StrEnum):
    SETSEED = "setSeed"

    def __str__(self) -> str:
        return str(self.value)
