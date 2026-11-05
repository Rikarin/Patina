from enum import StrEnum

class GraphCommandGraphAddParameterOp(StrEnum):
    ADDPARAMETER = "addParameter"

    def __str__(self) -> str:
        return str(self.value)
