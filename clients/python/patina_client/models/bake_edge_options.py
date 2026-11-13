from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset







T = TypeVar("T", bound="BakeEdgeOptions")



@_attrs_define
class BakeEdgeOptions:
    """ Which edges the edges map marks, and the band it draws along them (#148).

        Attributes:
            edges (str): `hard`, the edges the high mesh's own normals split, or `angle`, every edge whose faces meet at
                            double BakeEdgeOptions.Angle or more. With no high mesh the low mesh bakes onto itself, so these are
                its edges.
            width (float): How far either side of an edge the band reaches, in the rays' units.
            angle (float): For `angle`: the least angle between two faces that is an edge, in degrees.
            profile (str): How the band falls from white on the edge to black at its side: `even`, a straight ramp;
                `smooth`,
                a smoothstep; or `chamfer`, white all the way across.
     """

    edges: str
    width: float
    angle: float
    profile: str
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        edges = self.edges

        width = self.width

        angle = self.angle

        profile = self.profile


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "edges": edges,
            "width": width,
            "angle": angle,
            "profile": profile,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        edges = d.pop("edges")

        width = d.pop("width")

        angle = d.pop("angle")

        profile = d.pop("profile")

        bake_edge_options = cls(
            edges=edges,
            width=width,
            angle=angle,
            profile=profile,
        )


        bake_edge_options.additional_properties = d
        return bake_edge_options

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
