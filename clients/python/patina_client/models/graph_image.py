from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset







T = TypeVar("T", bound="GraphImage")



@_attrs_define
class GraphImage:
    """ Where a full-size picture is: `GET /api/v1/previews/{hash}`. Pixels never cross JSON.

        Attributes:
            hash_ (str): The SHA-256 of the PNG's bytes, in lower-case hex.
            width (int): The PNG's size in pixels.
            height (int): The PNG's size in pixels.
     """

    hash_: str
    width: int
    height: int
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        hash_ = self.hash_

        width = self.width

        height = self.height


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "hash": hash_,
            "width": width,
            "height": height,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        hash_ = d.pop("hash")

        width = d.pop("width")

        height = d.pop("height")

        graph_image = cls(
            hash_=hash_,
            width=width,
            height=height,
        )


        graph_image.additional_properties = d
        return graph_image

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
