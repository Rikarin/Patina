from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset







T = TypeVar("T", bound="BakedMapFile")



@_attrs_define
class BakedMapFile:
    """ One baked map, as a file.

        Attributes:
            map_ (str): What it measures.
            path (str): The file.
            resolution (int): Its edge in pixels.
            scale (float): What a stored unit value is multiplied by to get the measurement back, for a signed map in an
                integer format;
                zero when there is nothing to undo.
     """

    map_: str
    path: str
    resolution: int
    scale: float
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        map_ = self.map_

        path = self.path

        resolution = self.resolution

        scale = self.scale


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "map": map_,
            "path": path,
            "resolution": resolution,
            "scale": scale,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        map_ = d.pop("map")

        path = d.pop("path")

        resolution = d.pop("resolution")

        scale = d.pop("scale")

        baked_map_file = cls(
            map_=map_,
            path=path,
            resolution=resolution,
            scale=scale,
        )


        baked_map_file.additional_properties = d
        return baked_map_file

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
