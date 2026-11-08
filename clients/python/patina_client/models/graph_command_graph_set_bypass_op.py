from enum import StrEnum

class GraphCommandGraphSetBypassOp(StrEnum):
    SETBYPASS = "setBypass"

    def __str__(self) -> str:
        return str(self.value)
