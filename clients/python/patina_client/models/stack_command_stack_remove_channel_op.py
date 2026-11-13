from enum import StrEnum

class StackCommandStackRemoveChannelOp(StrEnum):
    REMOVECHANNEL = "removeChannel"

    def __str__(self) -> str:
        return str(self.value)
