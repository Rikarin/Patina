from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast

if TYPE_CHECKING:
  from ..models.baked_map_file import BakedMapFile





T = TypeVar("T", bound="BakeResult")



@_attrs_define
class BakeResult:
    """ What a finished bake made.

        Attributes:
            maps (list[BakedMapFile]): The files.
            covered (int): How many texels held chart content.
            missed (int): How many found no source and took the closest point instead.
            warnings (list[str]): What could not be baked as asked.
            cached (bool): Whether this was found in the cache and nothing ran.
            log (list[str]): What the bake read and found, line by line: the meshes, each part's match, the time it took.
     """

    maps: list[BakedMapFile]
    covered: int
    missed: int
    warnings: list[str]
    cached: bool
    log: list[str]
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.baked_map_file import BakedMapFile # noqa: PLC0415
        maps = []
        for maps_item_data in self.maps:
            maps_item = maps_item_data.to_dict()
            maps.append(maps_item)



        covered = self.covered

        missed = self.missed

        warnings = self.warnings



        cached = self.cached

        log = self.log




        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "maps": maps,
            "covered": covered,
            "missed": missed,
            "warnings": warnings,
            "cached": cached,
            "log": log,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.baked_map_file import BakedMapFile # noqa: PLC0415
        d = dict(src_dict)
        maps = []
        _maps = d.pop("maps")
        for maps_item_data in (_maps):
            maps_item = BakedMapFile.from_dict(maps_item_data)



            maps.append(maps_item)


        covered = d.pop("covered")

        missed = d.pop("missed")

        warnings = cast(list[str], d.pop("warnings"))


        cached = d.pop("cached")

        log = cast(list[str], d.pop("log"))


        bake_result = cls(
            maps=maps,
            covered=covered,
            missed=missed,
            warnings=warnings,
            cached=cached,
            log=log,
        )


        bake_result.additional_properties = d
        return bake_result

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
