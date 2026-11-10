from enum import StrEnum

class StackCommandStackSetSettingsOp(StrEnum):
    SETSETTINGS = "setSettings"

    def __str__(self) -> str:
        return str(self.value)
