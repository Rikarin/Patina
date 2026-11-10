from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast






T = TypeVar("T", bound="GraphInfoState")



@_attrs_define
class GraphInfoState:
    """ What a graph says about itself to whoever finds it on a shelf or in a package. Nothing it says changes what the
    graph makes.

        Attributes:
            description (str): What it makes and how it is meant to be used.
            author (str): Who made it.
            tags (list[str]): Words to find it by, beyond its name.
            physical_width (float | None | Unset): The width one tile stands for in the world, in centimetres, or null when
                unsaid.
            physical_height (float | None | Unset): Its height, the same way.
     """

    description: str
    author: str
    tags: list[str]
    physical_width: float | None | Unset = UNSET
    physical_height: float | None | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        description = self.description

        author = self.author

        tags = self.tags



        physical_width: float | None | Unset
        if isinstance(self.physical_width, Unset):
            physical_width = UNSET
        else:
            physical_width = self.physical_width

        physical_height: float | None | Unset
        if isinstance(self.physical_height, Unset):
            physical_height = UNSET
        else:
            physical_height = self.physical_height


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "description": description,
            "author": author,
            "tags": tags,
        })
        if physical_width is not UNSET:
            field_dict["physicalWidth"] = physical_width
        if physical_height is not UNSET:
            field_dict["physicalHeight"] = physical_height

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        description = d.pop("description")

        author = d.pop("author")

        tags = cast(list[str], d.pop("tags"))


        def _parse_physical_width(data: object) -> float | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(float | None | Unset, data)

        physical_width = _parse_physical_width(d.pop("physicalWidth", UNSET))


        def _parse_physical_height(data: object) -> float | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(float | None | Unset, data)

        physical_height = _parse_physical_height(d.pop("physicalHeight", UNSET))


        graph_info_state = cls(
            description=description,
            author=author,
            tags=tags,
            physical_width=physical_width,
            physical_height=physical_height,
        )


        graph_info_state.additional_properties = d
        return graph_info_state

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
