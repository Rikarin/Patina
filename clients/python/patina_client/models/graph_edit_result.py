from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast

if TYPE_CHECKING:
  from ..models.graph_created_node import GraphCreatedNode
  from ..models.graph_history_state import GraphHistoryState





T = TypeVar("T", bound="GraphEditResult")



@_attrs_define
class GraphEditResult:
    """ The reply to an edit, an undo, a redo or a save.

        Attributes:
            revision (int): The document's revision after it.
            sequence (int): The event that announced it.
            history (GraphHistoryState): What the undo history offers.
            created (list[GraphCreatedNode]): What a batch created under a `ref`.
     """

    revision: int
    sequence: int
    history: GraphHistoryState
    created: list[GraphCreatedNode]
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.graph_created_node import GraphCreatedNode # noqa: PLC0415
        from ..models.graph_history_state import GraphHistoryState # noqa: PLC0415
        revision = self.revision

        sequence = self.sequence

        history = self.history.to_dict()

        created = []
        for created_item_data in self.created:
            created_item = created_item_data.to_dict()
            created.append(created_item)




        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "revision": revision,
            "sequence": sequence,
            "history": history,
            "created": created,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.graph_created_node import GraphCreatedNode # noqa: PLC0415
        from ..models.graph_history_state import GraphHistoryState # noqa: PLC0415
        d = dict(src_dict)
        revision = d.pop("revision")

        sequence = d.pop("sequence")

        history = GraphHistoryState.from_dict(d.pop("history"))




        created = []
        _created = d.pop("created")
        for created_item_data in (_created):
            created_item = GraphCreatedNode.from_dict(created_item_data)



            created.append(created_item)


        graph_edit_result = cls(
            revision=revision,
            sequence=sequence,
            history=history,
            created=created,
        )


        graph_edit_result.additional_properties = d
        return graph_edit_result

    @property
    def additional_keys(self) -> list[str]:
        return list(self.additional_properties.keys())

    def __getitem__(self, key: str) -> Any:
        return self.additional_properties[key]

    def __setitem__(self, key: str, value: Any) -> None:
        self.additional_properties[key] = value

    def __delitem__(self, key: str) -> None:
        del self.additional_properties[key]

    def __contains__(self, key: str) -> bool:
        return key in self.additional_properties
