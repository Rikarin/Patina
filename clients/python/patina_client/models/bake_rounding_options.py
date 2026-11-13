from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset







T = TypeVar("T", bound="BakeRoundingOptions")



@_attrs_define
class BakeRoundingOptions:
    """ Edges baked round into the normal map, the bevel a hard-surface model was not modelled with.

        Attributes:
            enabled (bool): Whether edges are rounded at all.
            width (float): How far either side of an edge the bevel reaches, in the rays' units.
            edges (str): Which edges: `angle`, every edge whose faces meet at double BakeRoundingOptions.Angle or more, or
                `hard`, the
                edges the mesh's own normals split, which is the nearest a mesh file has to an edge weight.
            angle (float): For `angle`: the least angle between two faces that is an edge, in degrees.
            profile (str): How the normal turns across the bevel: `even`, a round fillet; `smooth`, flat at the bevel's side
                and turning fastest near the edge; or `chamfer`, one flat face at the halfway angle.
     """

    enabled: bool
    width: float
    edges: str
    angle: float
    profile: str
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        enabled = self.enabled

        width = self.width

        edges = self.edges

        angle = self.angle

        profile = self.profile


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "enabled": enabled,
            "width": width,
            "edges": edges,
            "angle": angle,
            "profile": profile,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        enabled = d.pop("enabled")

        width = d.pop("width")

        edges = d.pop("edges")

        angle = d.pop("angle")

        profile = d.pop("profile")

        bake_rounding_options = cls(
            enabled=enabled,
            width=width,
            edges=edges,
            angle=angle,
            profile=profile,
        )


        bake_rounding_options.additional_properties = d
        return bake_rounding_options

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
