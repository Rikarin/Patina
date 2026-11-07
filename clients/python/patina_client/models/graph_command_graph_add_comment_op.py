from enum import StrEnum

class GraphCommandGraphAddCommentOp(StrEnum):
    ADDCOMMENT = "addComment"

    def __str__(self) -> str:
        return str(self.value)
