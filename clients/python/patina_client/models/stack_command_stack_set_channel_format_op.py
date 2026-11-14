from enum import StrEnum

class StackCommandStackSetChannelFormatOp(StrEnum):
    SETCHANNELFORMAT = "setChannelFormat"

    def __str__(self) -> str:
        return str(self.value)
