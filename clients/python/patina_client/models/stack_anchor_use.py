from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset







T = TypeVar("T", bound="StackAnchorUse")



@_attrs_define
class StackAnchorUse:
    """ One place a stack reads an anchor point.

        Attributes:
            layer (str): The id of the layer that reads it.
            where (str): What part of that layer: fill, mask, mask entry 2, or an input's port.
            channel (str): The channel it reads, or empty for the channel being written.
     """

    layer: str
    where: str
    channel: str
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        layer = self.layer

        where = self.where

        channel = self.channel


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "layer": layer,
            "where": where,
            "channel": channel,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        layer = d.pop("layer")

        where = d.pop("where")

        channel = d.pop("channel")

        stack_anchor_use = cls(
            layer=layer,
            where=where,
            channel=channel,
        )


        stack_anchor_use.additional_properties = d
        return stack_anchor_use

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
