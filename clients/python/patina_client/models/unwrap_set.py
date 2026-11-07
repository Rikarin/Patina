from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast






T = TypeVar("T", bound="UnwrapSet")



@_attrs_define
class UnwrapSet:
    """ What one texture set's unwrap came to.

        Attributes:
            material (str): The material, which is the texture set.
            charts (int): How many islands. Fewer is better at the same stretch.
            flipped (int): Triangles turned over in the atlas. Anything but zero paints and bakes in the wrong place.
            stretch (float): The stretch averaged over area. 1 is none.
            worst_stretch (float): The stretch of the worst triangle.
            packed (float): The share of the atlas the islands cover, after the margin.
            warnings (list[str]): What a person should know: an island split, a rescale.
     """

    material: str
    charts: int
    flipped: int
    stretch: float
    worst_stretch: float
    packed: float
    warnings: list[str]
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        material = self.material

        charts = self.charts

        flipped = self.flipped

        stretch = self.stretch

        worst_stretch = self.worst_stretch

        packed = self.packed

        warnings = self.warnings




        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "material": material,
            "charts": charts,
            "flipped": flipped,
            "stretch": stretch,
            "worstStretch": worst_stretch,
            "packed": packed,
            "warnings": warnings,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        material = d.pop("material")

        charts = d.pop("charts")

        flipped = d.pop("flipped")

        stretch = d.pop("stretch")

        worst_stretch = d.pop("worstStretch")

        packed = d.pop("packed")

        warnings = cast(list[str], d.pop("warnings"))


        unwrap_set = cls(
            material=material,
            charts=charts,
            flipped=flipped,
            stretch=stretch,
            worst_stretch=worst_stretch,
            packed=packed,
            warnings=warnings,
        )


        unwrap_set.additional_properties = d
        return unwrap_set

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
