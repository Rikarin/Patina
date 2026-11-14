from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast






T = TypeVar("T", bound="StackChannel")



@_attrs_define
class StackChannel:
    """ A channel a stack writes.

        Attributes:
            usage (str): Its usage: baseColor, roughness and so on.
            default (list[float]): What it starts as.
            format_ (str): How wide the texels are that a stroke paints it at (#19): one of the vocabulary's formats, Rgba8
                unless the set
                says more. Default: 'Rgba8'.
     """

    usage: str
    default: list[float]
    format_: str = 'Rgba8'
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        usage = self.usage

        default = self.default



        format_ = self.format_


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "usage": usage,
            "default": default,
            "format": format_,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        usage = d.pop("usage")

        default = cast(list[float], d.pop("default"))


        format_ = d.pop("format")

        stack_channel = cls(
            usage=usage,
            default=default,
            format_=format_,
        )


        stack_channel.additional_properties = d
        return stack_channel

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
