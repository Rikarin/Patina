from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast






T = TypeVar("T", bound="StackBrush")



@_attrs_define
class StackBrush:
    """ A brush. Everything but the radius has the value a new brush has.

        Attributes:
            radius (float): In texels, or in the units of the picture for a stroke with a view.
            flow (float): How much one stamp lays down, 0 to 1. Default: 1.0.
            opacity (float): How much a whole stroke can lay down, 0 to 1. Default: 1.0.
            spacing (float): How far apart stamps are, as a share of the radius. Default: 0.15.
            falloff (float): How soft its edge is, 0 to 1. Default: 0.5.
            angle (float): Its fixed rotation, in radians. Default: 0.0.
            rotation (str): fixed, along or random. Default: 'fixed'.
            position_jitter (float): How far a stamp strays, as a share of the radius, 0 to 1. Default: 0.0.
            angle_jitter (float): How far a stamp turns either way, in radians. Default: 0.0.
            size_jitter (float): How much of its radius a stamp can lose, 0 to 1. Default: 0.0.
            pressure_size (float): How much of the radius pressure takes off, 0 to 1. Default: 0.0.
            pressure_flow (float): How much of the flow pressure takes off, 0 to 1. Default: 0.0.
            alpha (None | str | Unset): A named alpha (Round, Square, Chisel, Cross) or an image relative to the stack, or
                null.
     """

    radius: float
    flow: float = 1.0
    opacity: float = 1.0
    spacing: float = 0.15
    falloff: float = 0.5
    angle: float = 0.0
    rotation: str = 'fixed'
    position_jitter: float = 0.0
    angle_jitter: float = 0.0
    size_jitter: float = 0.0
    pressure_size: float = 0.0
    pressure_flow: float = 0.0
    alpha: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        radius = self.radius

        flow = self.flow

        opacity = self.opacity

        spacing = self.spacing

        falloff = self.falloff

        angle = self.angle

        rotation = self.rotation

        position_jitter = self.position_jitter

        angle_jitter = self.angle_jitter

        size_jitter = self.size_jitter

        pressure_size = self.pressure_size

        pressure_flow = self.pressure_flow

        alpha: None | str | Unset
        if isinstance(self.alpha, Unset):
            alpha = UNSET
        else:
            alpha = self.alpha


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "radius": radius,
            "flow": flow,
            "opacity": opacity,
            "spacing": spacing,
            "falloff": falloff,
            "angle": angle,
            "rotation": rotation,
            "positionJitter": position_jitter,
            "angleJitter": angle_jitter,
            "sizeJitter": size_jitter,
            "pressureSize": pressure_size,
            "pressureFlow": pressure_flow,
        })
        if alpha is not UNSET:
            field_dict["alpha"] = alpha

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        radius = d.pop("radius")

        flow = d.pop("flow")

        opacity = d.pop("opacity")

        spacing = d.pop("spacing")

        falloff = d.pop("falloff")

        angle = d.pop("angle")

        rotation = d.pop("rotation")

        position_jitter = d.pop("positionJitter")

        angle_jitter = d.pop("angleJitter")

        size_jitter = d.pop("sizeJitter")

        pressure_size = d.pop("pressureSize")

        pressure_flow = d.pop("pressureFlow")

        def _parse_alpha(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        alpha = _parse_alpha(d.pop("alpha", UNSET))


        stack_brush = cls(
            radius=radius,
            flow=flow,
            opacity=opacity,
            spacing=spacing,
            falloff=falloff,
            angle=angle,
            rotation=rotation,
            position_jitter=position_jitter,
            angle_jitter=angle_jitter,
            size_jitter=size_jitter,
            pressure_size=pressure_size,
            pressure_flow=pressure_flow,
            alpha=alpha,
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
