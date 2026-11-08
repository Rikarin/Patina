from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset







T = TypeVar("T", bound="StackAnchorRef")



@_attrs_define
class StackAnchorRef:
    """ A reference to an anchor point, by its name, in one channel.

        Attributes:
            anchor (str): The anchor point's name.
            channel (str): The channel read, by usage. Empty, for a fill or a filter's input only, means the channel being
                written. Default: ''.
     """

    anchor: str
    channel: str = ''
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        anchor = self.anchor

        channel = self.channel


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "anchor": anchor,
            "channel": channel,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        anchor = d.pop("anchor")

        channel = d.pop("channel")

        stack_anchor_ref = cls(
            anchor=anchor,
            channel=channel,
        )


        stack_anchor_ref.additional_properties = d
        return stack_anchor_ref

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
