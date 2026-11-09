from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset







T = TypeVar("T", bound="ProjectCheckSettings")



@_attrs_define
class ProjectCheckSettings:
    """ What a project holds its texture sets to before they are exported (#159), kept in its `project.yaml`. A
    zero is no limit, and a texel density of zero is the library's.

        Attributes:
            memory_budget (float): The most GPU memory one texture set's exported files may take, in mebibytes, or zero for
                no limit.
            max_size (int): The longest side a texture set's maps may have, in texels, or zero for no limit.
            texel_density (float): The texel density the sets are held to, in texels per unit of the model (a metre in
                glTF), or zero for the
                library's.
            density_tolerance (float): How far a set's density may stray from it before it is flagged, as a share: 0.25 is a
                quarter.
     """

    memory_budget: float
    max_size: int
    texel_density: float
    density_tolerance: float
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        memory_budget = self.memory_budget

        max_size = self.max_size

        texel_density = self.texel_density

        density_tolerance = self.density_tolerance


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "memoryBudget": memory_budget,
            "maxSize": max_size,
            "texelDensity": texel_density,
            "densityTolerance": density_tolerance,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        memory_budget = d.pop("memoryBudget")

        max_size = d.pop("maxSize")

        texel_density = d.pop("texelDensity")

        density_tolerance = d.pop("densityTolerance")

        project_check_settings = cls(
            memory_budget=memory_budget,
            max_size=max_size,
            texel_density=texel_density,
            density_tolerance=density_tolerance,
        )


        project_check_settings.additional_properties = d
        return project_check_settings

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
