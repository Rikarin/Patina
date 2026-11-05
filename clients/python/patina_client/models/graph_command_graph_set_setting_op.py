from enum import StrEnum

class GraphCommandGraphSetSettingOp(StrEnum):
    SETSETTING = "setSetting"

    def __str__(self) -> str:
        return str(self.value)
