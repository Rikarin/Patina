from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset







T = TypeVar("T", bound="StackThumbnail")



@_attrs_define
class StackThumbnail:
    """ What a layer looks like alone.

        Attributes:
            layer (str): The layer.
            hash_ (str): Its name under `/previews/{hash}`.
     """

    layer: str
    hash_: str
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        layer = self.layer

        hash_ = self.hash_


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "layer": layer,
            "hash": hash_,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        layer = d.pop("layer")

        hash_ = d.pop("hash")

        stack_thumbnail = cls(
            layer=layer,
            hash_=hash_,
        )


        stack_thumbnail.additional_properties = d
        return stack_thumbnail

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
