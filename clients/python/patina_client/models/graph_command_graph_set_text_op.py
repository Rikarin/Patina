from enum import StrEnum

class GraphCommandGraphSetTextOp(StrEnum):
    SETTEXT = "setText"

    def __str__(self) -> str:
        return str(self.value)
