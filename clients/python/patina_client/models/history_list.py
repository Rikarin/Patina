from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast

if TYPE_CHECKING:
  from ..models.history_step import HistoryStep





T = TypeVar("T", bound="HistoryList")



@_attrs_define
class HistoryList:
    """ An undo history as a list, which is what a history pane shows.

        Attributes:
            done (list[HistoryStep]): The steps that stand, oldest first. Undo takes back the last.
            undone (list[HistoryStep]): The steps undone, the next one redo puts back first.
     """

    done: list[HistoryStep]
    undone: list[HistoryStep]
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.history_step import HistoryStep # noqa: PLC0415
        done = []
        for done_item_data in self.done:
            done_item = done_item_data.to_dict()
            done.append(done_item)



        undone = []
        for undone_item_data in self.undone:
            undone_item = undone_item_data.to_dict()
            undone.append(undone_item)




        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "done": done,
            "undone": undone,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.history_step import HistoryStep # noqa: PLC0415
        d = dict(src_dict)
        done = []
        _done = d.pop("done")
        for done_item_data in (_done):
            done_item = HistoryStep.from_dict(done_item_data)



            done.append(done_item)


        undone = []
        _undone = d.pop("undone")
        for undone_item_data in (_undone):
            undone_item = HistoryStep.from_dict(undone_item_data)



            undone.append(undone_item)


        history_list = cls(
            done=done,
            undone=undone,
        )


        history_list.additional_properties = d
        return history_list

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
