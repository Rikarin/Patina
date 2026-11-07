from enum import StrEnum

class StackCommandStackDuplicateLayersOp(StrEnum):
    DUPLICATELAYERS = "duplicateLayers"

    def __str__(self) -> str:
        return str(self.value)
