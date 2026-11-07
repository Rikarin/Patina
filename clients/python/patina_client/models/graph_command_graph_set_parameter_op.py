from enum import StrEnum

class GraphCommandGraphSetParameterOp(StrEnum):
    SETPARAMETER = "setParameter"

    def __str__(self) -> str:
        return str(self.value)
