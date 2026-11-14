from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset







T = TypeVar("T", bound="PlatformCheckLimits")



@_attrs_define
class PlatformCheckLimits:
    """ What one platform holds a project's texture sets to (#159), named by the export preset that writes for it. A
    zero is the project's own limit.

        Attributes:
            preset (str): The export preset's id.
            memory_budget (float): The most GPU memory one set's files may take on it, in mebibytes, or zero for the
                project's.
            max_size (int): The longest side a set's maps may have on it, in texels, or zero for the project's.
            texel_density (float): The texel density it holds sets to, or zero for the project's.
     """

    preset: str
    memory_budget: float
    max_size: int
    texel_density: float
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        preset = self.preset

        memory_budget = self.memory_budget

        max_size = self.max_size

        texel_density = self.texel_density


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "preset": preset,
            "memoryBudget": memory_budget,
            "maxSize": max_size,
            "texelDensity": texel_density,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        preset = d.pop("preset")

        memory_budget = d.pop("memoryBudget")

        max_size = d.pop("maxSize")

        texel_density = d.pop("texelDensity")

        platform_check_limits = cls(
            preset=preset,
            memory_budget=memory_budget,
            max_size=max_size,
            texel_density=texel_density,
        )


        platform_check_limits.additional_properties = d
        return platform_check_limits

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
