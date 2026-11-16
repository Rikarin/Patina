from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast

if TYPE_CHECKING:
  from ..models.platform_check_limits import PlatformCheckLimits





T = TypeVar("T", bound="ProjectCheckSettings")



@_attrs_define
class ProjectCheckSettings:
    """ What a project holds its texture sets to before they are exported (#159), kept in its `project.yaml`. A
    zero is no limit, and a texel density of zero is the library's.

        Attributes:
            memory_budget (float): The most GPU memory one texture set's exported files may take, in mebibytes, or zero for
                no limit. Default: 0.0.
            max_size (int): The longest side a texture set's maps may have, in texels, or zero for no limit. Default: 0.
            texel_density (float): The texel density the sets are held to, in texels per metre, or zero for the library's. A
                mesh's own unit is
                turned into metres by what its file or project says it is (#307). Default: 0.0.
            density_tolerance (float): How far a set's density may stray from it before it is flagged, as a share: 0.25 is a
                quarter. Default: 0.25.
            platforms (list[PlatformCheckLimits]): Limits of their own for the platforms the project ships to, each named by
                the export preset that writes for
                it. A set measured with that preset is held to them in place of the project's.
     """

    platforms: list[PlatformCheckLimits]
    memory_budget: float = 0.0
    max_size: int = 0
    texel_density: float = 0.0
    density_tolerance: float = 0.25
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.platform_check_limits import PlatformCheckLimits # noqa: PLC0415
        memory_budget = self.memory_budget

        max_size = self.max_size

        texel_density = self.texel_density

        density_tolerance = self.density_tolerance

        platforms = []
        for platforms_item_data in self.platforms:
            platforms_item = platforms_item_data.to_dict()
            platforms.append(platforms_item)




        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "memoryBudget": memory_budget,
            "maxSize": max_size,
            "texelDensity": texel_density,
            "densityTolerance": density_tolerance,
            "platforms": platforms,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.platform_check_limits import PlatformCheckLimits # noqa: PLC0415
        d = dict(src_dict)
        memory_budget = d.pop("memoryBudget")

        max_size = d.pop("maxSize")

        texel_density = d.pop("texelDensity")

        density_tolerance = d.pop("densityTolerance")

        platforms = []
        _platforms = d.pop("platforms")
        for platforms_item_data in (_platforms):
            platforms_item = PlatformCheckLimits.from_dict(platforms_item_data)



            platforms.append(platforms_item)


        project_check_settings = cls(
            memory_budget=memory_budget,
            max_size=max_size,
            texel_density=texel_density,
            density_tolerance=density_tolerance,
            platforms=platforms,
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
