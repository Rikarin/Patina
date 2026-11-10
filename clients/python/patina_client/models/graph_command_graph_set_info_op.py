from enum import StrEnum

class GraphCommandGraphSetInfoOp(StrEnum):
    SETINFO = "setInfo"

    def __str__(self) -> str:
        return str(self.value)
