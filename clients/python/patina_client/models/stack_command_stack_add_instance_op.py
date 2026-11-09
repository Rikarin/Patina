from enum import StrEnum

class StackCommandStackAddInstanceOp(StrEnum):
    ADDINSTANCE = "addInstance"

    def __str__(self) -> str:
        return str(self.value)
