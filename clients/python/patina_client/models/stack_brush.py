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
            radius (float): In SizeSpace: by default texels for a stroke with no view and the units of the picture for
                one with a view. Above zero and at most float StackBrushes.MaximumRadius.
            flow (float): How much one stamp lays down, 0 to 1. Default: 1.0.
            opacity (float): How much a whole stroke can lay down, 0 to 1. Default: 1.0.
            spacing (float): How far apart stamps are, as a share of the radius: above zero, and above 2 leaves gaps.
                Default: 0.15.
            falloff (float): How soft its edge is, 0 to 1. Default: 0.5.
            angle (float): Its fixed rotation, in radians. Default: 0.0.
            rotation (str): fixed, along or random. Default: 'fixed'.
            position_jitter (float): How far a stamp strays, as a share of the radius, 0 to 1. Default: 0.0.
            angle_jitter (float): How far a stamp turns either way, in radians. Default: 0.0.
            size_jitter (float): How much of its radius a stamp can lose, 0 to 1. Default: 0.0.
            pressure_size (float): How much of the radius pressure takes off, 0 to 1. Default: 0.0.
            pressure_flow (float): How much of the flow pressure takes off, 0 to 1. Default: 0.0.
            lazy (float): How far the brush trails the pointer, in the units of the radius: a lazy mouse. 0 follows it. A
                line or a path
                takes none, so that it reaches its last point. Default: 0.0.
            curve (str): How its edge falls away: smooth, linear, spherical or tip. Default: 'smooth'.
            aspect (float): How much longer than wide a stamp is, 1 to 16; 1 is round. It keeps the area of the round stamp,
                so a longer one
                is narrower. Default: 1.0.
            aspect_angle (float): Which way the long side points, in radians, on the picture or the map it is made on.
                Default: 0.0.
            flow_jitter (float): How much of its flow a stamp can lose, 0 to 1. Default: 0.0.
            scatter_axis (str): Which way PositionJitter moves a stamp: both, or across the stroke only. Default: 'both'.
            accumulate (bool): Whether stamps build up where they overlap, to the opacity. Off, a place keeps the strongest
                stamp to reach it,
                so a slow drag paints what a fast one does. Default: True.
            size_space (str): What the radius is measured in: `screen`, the stroke's own picture or map; `texture`, texels
                of the
                map whatever the view; or `model`, thousandths of the model's size (its bounds' diagonal), which needs a
                mesh. A size other than screen is measured where the stroke starts and kept for the whole of it. Default:
                'screen'.
            occlusion (bool): Whether a stroke with a view paints only what its camera sees. Off, every texel within the
                depth under the brush
                is painted, hidden or not: a face close under a strap, the inside of a lip. Default: True.
            occlusion_tolerance (float): How far in front of a texel something may lie and still not hide it, in thousandths
                of the model's size (its
                bounds' diagonal), 0 to 1000: a decal floating a hair over a face is not the face's occluder. Default: 2.0.
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
    lazy: float = 0.0
    curve: str = 'smooth'
    aspect: float = 1.0
    aspect_angle: float = 0.0
    flow_jitter: float = 0.0
    scatter_axis: str = 'both'
    accumulate: bool = True
    size_space: str = 'screen'
    occlusion: bool = True
    occlusion_tolerance: float = 2.0
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

        lazy = self.lazy

        curve = self.curve

        aspect = self.aspect

        aspect_angle = self.aspect_angle

        flow_jitter = self.flow_jitter

        scatter_axis = self.scatter_axis

        accumulate = self.accumulate

        size_space = self.size_space

        occlusion = self.occlusion

        occlusion_tolerance = self.occlusion_tolerance

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
            "lazy": lazy,
            "curve": curve,
            "aspect": aspect,
            "aspectAngle": aspect_angle,
            "flowJitter": flow_jitter,
            "scatterAxis": scatter_axis,
            "accumulate": accumulate,
            "sizeSpace": size_space,
            "occlusion": occlusion,
            "occlusionTolerance": occlusion_tolerance,
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

        lazy = d.pop("lazy")

        curve = d.pop("curve")

        aspect = d.pop("aspect")

        aspect_angle = d.pop("aspectAngle")

        flow_jitter = d.pop("flowJitter")

        scatter_axis = d.pop("scatterAxis")

        accumulate = d.pop("accumulate")

        size_space = d.pop("sizeSpace")

        occlusion = d.pop("occlusion")

        occlusion_tolerance = d.pop("occlusionTolerance")

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
            lazy=lazy,
            curve=curve,
            aspect=aspect,
            aspect_angle=aspect_angle,
            flow_jitter=flow_jitter,
            scatter_axis=scatter_axis,
            accumulate=accumulate,
            size_space=size_space,
            occlusion=occlusion,
            occlusion_tolerance=occlusion_tolerance,
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
