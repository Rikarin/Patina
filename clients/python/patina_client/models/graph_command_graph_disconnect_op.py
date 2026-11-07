from enum import StrEnum

class GraphCommandGraphDisconnectOp(StrEnum):
    DISCONNECT = "disconnect"

    def __str__(self) -> str:
        return str(self.value)
