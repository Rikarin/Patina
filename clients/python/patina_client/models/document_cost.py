from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset







T = TypeVar("T", bound="DocumentCost")



@_attrs_define
class DocumentCost:
    """ One document and what it costs.

        Attributes:
            path (str): Its file, below the library.
            width (int): Its width in texels.
            height (int): Its height in texels.
            maps (int): How many maps its texture sets export.
            bytes_ (int): What they hold together, uncompressed.
     """

    path: str
    width: int
    height: int
    maps: int
    bytes_: int
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        path = self.path

        width = self.width

        height = self.height

        maps = self.maps

        bytes_ = self.bytes_


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "path": path,
            "width": width,
            "height": height,
            "maps": maps,
            "bytes": bytes_,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        path = d.pop("path")

        width = d.pop("width")

        height = d.pop("height")

        maps = d.pop("maps")

        bytes_ = d.pop("bytes")

        document_cost = cls(
            path=path,
            width=width,
            height=height,
            maps=maps,
            bytes_=bytes_,
        )


        document_cost.additional_properties = d
        return document_cost

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
