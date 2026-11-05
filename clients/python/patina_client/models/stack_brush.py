from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset







T = TypeVar("T", bound="StackBrush")



@_attrs_define
class StackBrush:
    """ A brush. Everything but the radius has the value a new brush has.

        Attributes:
            radius (float): In texels.
            flow (float): How much one stamp lays down, 0 to 1. Default: 1.0.
            opacity (float): How much a whole stroke can lay down, 0 to 1. Default: 1.0.
            spacing (float): How far apart stamps are, as a share of the radius. Default: 0.15.
            falloff (float): How soft its edge is, 0 to 1. Default: 0.5.
     """

    radius: float
    flow: float = 1.0
    opacity: float = 1.0
    spacing: float = 0.15
    falloff: float = 0.5
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        radius = self.radius

        flow = self.flow

        opacity = self.opacity

        spacing = self.spacing

        falloff = self.falloff


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "radius": radius,
            "flow": flow,
            "opacity": opacity,
            "spacing": spacing,
            "falloff": falloff,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        radius = d.pop("radius")

        flow = d.pop("flow")

        opacity = d.pop("opacity")

        spacing = d.pop("spacing")

        falloff = d.pop("falloff")

        stack_brush = cls(
            radius=radius,
            flow=flow,
            opacity=opacity,
            spacing=spacing,
            falloff=falloff,
        )


        stack_brush.additional_properties = d
        return stack_brush

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
