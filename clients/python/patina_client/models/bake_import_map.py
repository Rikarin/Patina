from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast






T = TypeVar("T", bound="BakeImportMap")



@_attrs_define
class BakeImportMap:
    """ One map brought in.

        Attributes:
            map_ (str): One of the maps a bake makes, such as `normal`, `ao` or `curvature`.
            file (str): The picture, absolute or below the library: a PNG is copied as it is, and any other picture Patina
                reads is
                written as a 16-bit PNG by the GPU engine (#300).
            green_down (bool | None | Unset): For a tangent-space `normal` or `bent` map: its green runs down the picture,
                as DirectX's do, and is
                turned over as it comes in, since Patina's bakes run it up. False keeps it as it is. Null, or left out, reads
                it as the project's string ProjectContents.NormalFormat says (#144).
     """

    map_: str
    file: str
    green_down: bool | None | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        map_ = self.map_

        file = self.file

        green_down: bool | None | Unset
        if isinstance(self.green_down, Unset):
            green_down = UNSET
        else:
            green_down = self.green_down


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "map": map_,
            "file": file,
        })
        if green_down is not UNSET:
            field_dict["greenDown"] = green_down

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        map_ = d.pop("map")

        file = d.pop("file")

        def _parse_green_down(data: object) -> bool | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(bool | None | Unset, data)

        green_down = _parse_green_down(d.pop("greenDown", UNSET))


        bake_import_map = cls(
            map_=map_,
            file=file,
            green_down=green_down,
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
