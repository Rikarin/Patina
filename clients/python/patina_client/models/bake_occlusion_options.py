from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset







T = TypeVar("T", bound="BakeOcclusionOptions")



@_attrs_define
class BakeOcclusionOptions:
    """ The occlusion rays, which ambient occlusion, the bent normal and thickness share.

        Attributes:
            rays (int): How many rays a texel's occlusion is estimated with.
            distance (float): How far an occluder counts, in the rays' units.
            spread (float): The half-angle of the cone the rays fill, in degrees: 90 is the hemisphere.
            falloff (str): How an occluder counts for less further away: `none`, `linear` or `smooth`.
            distribution (str): How the rays are spread: `cosine` or `uniform`.
            minimum (float): How near an occluder may be and not count, in the rays' units.
            ignore_backfaces (bool): Whether a ray passes through the back of a face.
            within_part (bool): Whether only the texel's own part occludes it, when parts are matched by name.
            ground_plane (bool): Whether a floor under the high mesh occludes too. Y is up.
            ground_offset (float): How far above the high mesh's lowest point the floor is, in the rays' units.
     """

    rays: int
    distance: float
    spread: float
    falloff: str
    distribution: str
    minimum: float
    ignore_backfaces: bool
    within_part: bool
    ground_plane: bool
    ground_offset: float
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        rays = self.rays

        distance = self.distance

        spread = self.spread

        falloff = self.falloff

        distribution = self.distribution

        minimum = self.minimum

        ignore_backfaces = self.ignore_backfaces

        within_part = self.within_part

        ground_plane = self.ground_plane

        ground_offset = self.ground_offset


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "rays": rays,
            "distance": distance,
            "spread": spread,
            "falloff": falloff,
            "distribution": distribution,
            "minimum": minimum,
            "ignoreBackfaces": ignore_backfaces,
            "withinPart": within_part,
            "groundPlane": ground_plane,
            "groundOffset": ground_offset,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        rays = d.pop("rays")

        distance = d.pop("distance")

        spread = d.pop("spread")

        falloff = d.pop("falloff")

        distribution = d.pop("distribution")

        minimum = d.pop("minimum")

        ignore_backfaces = d.pop("ignoreBackfaces")

        within_part = d.pop("withinPart")

        ground_plane = d.pop("groundPlane")

        ground_offset = d.pop("groundOffset")

        bake_occlusion_options = cls(
            rays=rays,
            distance=distance,
            spread=spread,
            falloff=falloff,
            distribution=distribution,
            minimum=minimum,
            ignore_backfaces=ignore_backfaces,
            within_part=within_part,
            ground_plane=ground_plane,
            ground_offset=ground_offset,
        )


        bake_occlusion_options.additional_properties = d
        return bake_occlusion_options

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
