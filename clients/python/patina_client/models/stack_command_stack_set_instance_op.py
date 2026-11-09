from enum import StrEnum

class StackCommandStackSetInstanceOp(StrEnum):
    SETINSTANCE = "setInstance"

    def __str__(self) -> str:
        return str(self.value)
