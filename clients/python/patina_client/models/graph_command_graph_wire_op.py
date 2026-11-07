from enum import StrEnum

class GraphCommandGraphWireOp(StrEnum):
    WIRE = "wire"

    def __str__(self) -> str:
        return str(self.value)
