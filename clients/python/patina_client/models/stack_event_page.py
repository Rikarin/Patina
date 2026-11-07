from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast

if TYPE_CHECKING:
  from ..models.stack_event import StackEvent





T = TypeVar("T", bound="StackEventPage")



@_attrs_define
class StackEventPage:
    """ The events after a number.

        Attributes:
            sequence (int): The newest event's number.
            events (list[StackEvent]): The events.
     """

    sequence: int
    events: list[StackEvent]
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.stack_event import StackEvent # noqa: PLC0415
        sequence = self.sequence

        events = []
        for events_item_data in self.events:
            events_item = events_item_data.to_dict()
            events.append(events_item)




        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "sequence": sequence,
            "events": events,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.stack_event import StackEvent # noqa: PLC0415
        d = dict(src_dict)
        sequence = d.pop("sequence")

        events = []
        _events = d.pop("events")
        for events_item_data in (_events):
            events_item = StackEvent.from_dict(events_item_data)



            events.append(events_item)


        stack_event_page = cls(
            sequence=sequence,
            events=events,
        )


        stack_event_page.additional_properties = d
        return stack_event_page

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
