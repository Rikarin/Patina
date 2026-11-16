from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast






T = TypeVar("T", bound="BakeCurvatureOptions")



@_attrs_define
class BakeCurvatureOptions:
    """ The curvature map's source and stored range (#148).

        Attributes:
            source (str): Where the bends come from: `mesh`, the high mesh's own faces where each ray lands;
                `normal-map`, the bends of the baked normal map, which is the high mesh's detail over the low mesh,
                down to its smallest grooves, and none of the low mesh's own shape; or `rays`, secondary rays
                double BakeCurvatureOptions.Sampling round where each ray lands, a bend at that scale, so a hard edge of a low
                poly high
                mesh reads as one. Default: 'mesh'.
            range_ (str): `auto`, scaled so 99 texels in a hundred keep their gradient, which differs from mesh to mesh; or
                            `custom`, where a bend as tight as double BakeCurvatureOptions.Radius reads white, or black for a
                crease, so every
                            bake made with it agrees. Default: 'auto'.
            radius (float): For `custom`: the radius of the tightest bend the map tells apart, in the rays' units. Default:
                0.01.
            sampling (float): For `rays`: how far round where each ray lands the secondary rays look, in the rays' units,
                which is
                the radius of the bend read and the finest one told apart. Default: 0.01.
            rays (int): For `rays`: how many secondary rays each sample casts, 1 to 256. Default: 16.
            within_part (bool | None | Unset): For `rays`, when parts are matched by name: whether the secondary rays meet
                only the texel's own part
                (#300), so a part standing on another reads no crease where they meet, as an exploded bake would. Null or
                false reads the whole high mesh.
     """

    source: str = 'mesh'
    range_: str = 'auto'
    radius: float = 0.01
    sampling: float = 0.01
    rays: int = 16
    within_part: bool | None | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        source = self.source

        range_ = self.range_

        radius = self.radius

        sampling = self.sampling

        rays = self.rays

        within_part: bool | None | Unset
        if isinstance(self.within_part, Unset):
            within_part = UNSET
        else:
            within_part = self.within_part


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "source": source,
            "range": range_,
            "radius": radius,
            "sampling": sampling,
            "rays": rays,
        })
        if within_part is not UNSET:
            field_dict["withinPart"] = within_part

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        source = d.pop("source")

        range_ = d.pop("range")

        radius = d.pop("radius")

        sampling = d.pop("sampling")

        rays = d.pop("rays")

        def _parse_within_part(data: object) -> bool | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(bool | None | Unset, data)

        within_part = _parse_within_part(d.pop("withinPart", UNSET))


        bake_curvature_options = cls(
            source=source,
            range_=range_,
            radius=radius,
            sampling=sampling,
            rays=rays,
            within_part=within_part,
        )


        bake_curvature_options.additional_properties = d
        return bake_curvature_options

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
