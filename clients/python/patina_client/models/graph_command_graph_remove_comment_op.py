from enum import StrEnum

class GraphCommandGraphRemoveCommentOp(StrEnum):
    REMOVECOMMENT = "removeComment"

    def __str__(self) -> str:
        return str(self.value)
