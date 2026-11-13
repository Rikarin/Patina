from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast






T = TypeVar("T", bound="BakedMapFile")



@_attrs_define
class BakedMapFile:
    """ One baked map, as a file.

        Attributes:
            map_ (str): What it measures.
            path (str): The file.
            resolution (int): Its width in pixels.
            scale (float): What a stored unit value is multiplied by to get the measurement back, for a signed map in an
                integer format;
                zero when there is nothing to undo.
            rows (int | None | Unset): Its height in pixels, or null when it is as tall as it is wide.
     """

    map_: str
    path: str
    resolution: int
    scale: float
    rows: int | None | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        map_ = self.map_

        path = self.path

        resolution = self.resolution

        scale = self.scale

        rows: int | None | Unset
        if isinstance(self.rows, Unset):
            rows = UNSET
        else:
            rows = self.rows


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "map": map_,
            "path": path,
            "resolution": resolution,
            "scale": scale,
        })
        if rows is not UNSET:
            field_dict["rows"] = rows

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        map_ = d.pop("map")

        path = d.pop("path")

        resolution = d.pop("resolution")

        scale = d.pop("scale")

        def _parse_rows(data: object) -> int | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(int | None | Unset, data)

        rows = _parse_rows(d.pop("rows", UNSET))


        baked_map_file = cls(
            map_=map_,
            path=path,
            resolution=resolution,
            scale=scale,
            rows=rows,
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
