from enum import StrEnum

class GraphCommandGraphConnectOp(StrEnum):
    CONNECT = "connect"

    def __str__(self) -> str:
        return str(self.value)
