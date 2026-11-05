from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset







T = TypeVar("T", bound="StackPreview")



@_attrs_define
class StackPreview:
    """ A map as a small picture, by the hash an event carries.

        Attributes:
            usage (str): Which channel.
            hash_ (str): Its name under `/previews/{hash}`.
            width (int): Its width in pixels.
            height (int): Its height in pixels.
     """

    usage: str
    hash_: str
    width: int
    height: int
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        usage = self.usage

        hash_ = self.hash_

        width = self.width

        height = self.height


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "usage": usage,
            "hash": hash_,
            "width": width,
            "height": height,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        usage = d.pop("usage")

        hash_ = d.pop("hash")

        width = d.pop("width")

        height = d.pop("height")

        stack_preview = cls(
            usage=usage,
            hash_=hash_,
            width=width,
            height=height,
        )


        stack_preview.additional_properties = d
        return stack_preview

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
