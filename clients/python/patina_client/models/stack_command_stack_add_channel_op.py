from enum import StrEnum

class StackCommandStackAddChannelOp(StrEnum):
    ADDCHANNEL = "addChannel"

    def __str__(self) -> str:
        return str(self.value)
