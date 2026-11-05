from enum import StrEnum

class GraphCommandGraphAddPortOp(StrEnum):
    ADDPORT = "addPort"

    def __str__(self) -> str:
        return str(self.value)
