from enum import StrEnum

class GraphCommandGraphSetCommentOp(StrEnum):
    SETCOMMENT = "setComment"

    def __str__(self) -> str:
        return str(self.value)
