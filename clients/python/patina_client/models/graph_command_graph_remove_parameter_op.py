from enum import StrEnum

class GraphCommandGraphRemoveParameterOp(StrEnum):
    REMOVEPARAMETER = "removeParameter"

    def __str__(self) -> str:
        return str(self.value)
