from enum import StrEnum

class GraphCommandGraphSetValueOp(StrEnum):
    SETVALUE = "setValue"

    def __str__(self) -> str:
        return str(self.value)
