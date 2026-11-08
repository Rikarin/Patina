from enum import StrEnum

class GraphCommandGraphArrangeOp(StrEnum):
    ARRANGE = "arrange"

    def __str__(self) -> str:
        return str(self.value)
