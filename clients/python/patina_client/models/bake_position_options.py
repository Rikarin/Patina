from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset







T = TypeVar("T", bound="BakePositionOptions")



@_attrs_define
class BakePositionOptions:
    """ How the position map turns a point on the high poly mesh into a value (#148).

        Attributes:
            range_ (str): `box`, each axis across the mesh's bounding box, so each channel is used whole and shapes stretch;
                            `cube`, every axis across the box's longest side, so distances agree; `sphere`, across the sphere
                            round the box, centred on its middle; or `none`, the point in model units, which only EXR keeps.
     """

    range_: str
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        range_ = self.range_


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "range": range_,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        range_ = d.pop("range")

        bake_position_options = cls(
            range_=range_,
        )


        bake_position_options.additional_properties = d
        return bake_position_options

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
