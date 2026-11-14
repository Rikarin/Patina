from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset







T = TypeVar("T", bound="BakeCurvatureOptions")



@_attrs_define
class BakeCurvatureOptions:
    """ The curvature map's source and stored range (#148).

        Attributes:
            source (str): Where the bends come from: `mesh`, the high mesh's own faces where each ray lands; or
                `normal-map`, the bends of the baked normal map, which is the high mesh's detail over the low mesh,
                down to its smallest grooves, and none of the low mesh's own shape. Default: 'mesh'.
            range_ (str): `auto`, scaled so 99 texels in a hundred keep their gradient, which differs from mesh to mesh; or
                            `custom`, where a bend as tight as double BakeCurvatureOptions.Radius reads white, or black for a
                crease, so every
                            bake made with it agrees. Default: 'auto'.
            radius (float): For `custom`: the radius of the tightest bend the map tells apart, in the rays' units. Default:
                0.01.
     """

    source: str = 'mesh'
    range_: str = 'auto'
    radius: float = 0.01
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        source = self.source

        range_ = self.range_

        radius = self.radius


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "source": source,
            "range": range_,
            "radius": radius,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        source = d.pop("source")

        range_ = d.pop("range")

        radius = d.pop("radius")

        bake_curvature_options = cls(
            source=source,
            range_=range_,
            radius=radius,
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
