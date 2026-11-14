from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset







T = TypeVar("T", bound="BakeImportMap")



@_attrs_define
class BakeImportMap:
    """ One map brought in.

        Attributes:
            map_ (str): One of the maps a bake makes, such as `normal`, `ao` or `curvature`.
            file (str): The PNG, absolute or below the library. A normal map's green runs up the picture, as Patina's bakes
                do.
     """

    map_: str
    file: str
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        map_ = self.map_

        file = self.file


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "map": map_,
            "file": file,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        map_ = d.pop("map")

        file = d.pop("file")

        bake_import_map = cls(
            map_=map_,
            file=file,
        )


        bake_import_map.additional_properties = d
        return bake_import_map

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
