from enum import StrEnum

class StackCommandStackSetUserChannelOp(StrEnum):
    SETUSERCHANNEL = "setUserChannel"

    def __str__(self) -> str:
        return str(self.value)
