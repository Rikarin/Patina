from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast

if TYPE_CHECKING:
  from ..models.stack_path_point import StackPathPoint





T = TypeVar("T", bound="StackSurfacePath")



@_attrs_define
class StackSurfacePath:
    """ A curve on the model that a fill with the `Path` projection is laid along (#130, #131): the points stay in
    the stack, so they can be moved after the stroke is seen, and the stroke follows. It passes through each point.

        Attributes:
            points (list[StackPathPoint]): The points, in order.
            radius (float): Half the path's width at full pressure, in the model's units. Above zero.
            hardness (float): How much of the width is at full strength before the edge falls away, 0 to 1. Default: 0.5.
            ribbon (bool): Whether the fill's picture runs along the path, its height across the path's width, rather than
                showing through
                the path where it lies on the map. Default: False.
            repeat (float): For a ribbon, how far along the path one repeat of the picture runs, in the model's units. Zero
                keeps the
                picture's own shape at the path's width. Default: 0.0.
            angle (float): How far a face may turn from the path's normal and still take it, in radians, 0 to π. Default:
                1.3.
            box (list[float] | None | Unset): The box the mesh's `position` bake is normalised in, as a decal's is.
     """

    points: list[StackPathPoint]
    radius: float
    hardness: float = 0.5
    ribbon: bool = False
    repeat: float = 0.0
    angle: float = 1.3
    box: list[float] | None | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.stack_path_point import StackPathPoint # noqa: PLC0415
        points = []
        for points_item_data in self.points:
            points_item = points_item_data.to_dict()
            points.append(points_item)



        radius = self.radius

        hardness = self.hardness

        ribbon = self.ribbon

        repeat = self.repeat

        angle = self.angle

        box: list[float] | None | Unset
        if isinstance(self.box, Unset):
            box = UNSET
        elif isinstance(self.box, list):
            box = self.box


        else:
            box = self.box


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "points": points,
            "radius": radius,
            "hardness": hardness,
            "ribbon": ribbon,
            "repeat": repeat,
            "angle": angle,
        })
        if box is not UNSET:
            field_dict["box"] = box

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.stack_path_point import StackPathPoint # noqa: PLC0415
        d = dict(src_dict)
        points = []
        _points = d.pop("points")
        for points_item_data in (_points):
            points_item = StackPathPoint.from_dict(points_item_data)



            points.append(points_item)


        radius = d.pop("radius")

        hardness = d.pop("hardness")

        ribbon = d.pop("ribbon")

        repeat = d.pop("repeat")

        angle = d.pop("angle")

        def _parse_box(data: object) -> list[float] | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, list):
                    raise TypeError()
                box_type_1 = cast(list[float], data)

                return box_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(list[float] | None | Unset, data)

        box = _parse_box(d.pop("box", UNSET))


        stack_surface_path = cls(
            points=points,
            radius=radius,
            hardness=hardness,
            ribbon=ribbon,
            repeat=repeat,
            angle=angle,
            box=box,
        )


        stack_surface_path.additional_properties = d
        return stack_surface_path

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
