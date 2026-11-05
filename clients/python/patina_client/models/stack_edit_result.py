from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast

if TYPE_CHECKING:
  from ..models.graph_history_state import GraphHistoryState





T = TypeVar("T", bound="StackEditResult")



@_attrs_define
class StackEditResult:
    """ Where an edit left the session.

        Attributes:
            revision (int): The revision of the document.
            sequence (int): The event it produced.
            history (GraphHistoryState): What the undo history offers.
     """

    revision: int
    sequence: int
    history: GraphHistoryState
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.graph_history_state import GraphHistoryState # noqa: PLC0415
        revision = self.revision

        sequence = self.sequence

        history = self.history.to_dict()


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "revision": revision,
            "sequence": sequence,
            "history": history,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.graph_history_state import GraphHistoryState # noqa: PLC0415
        d = dict(src_dict)
        revision = d.pop("revision")

        sequence = d.pop("sequence")

        history = GraphHistoryState.from_dict(d.pop("history"))




        stack_edit_result = cls(
            revision=revision,
            sequence=sequence,
            history=history,
        )


        stack_edit_result.additional_properties = d
        return stack_edit_result

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
