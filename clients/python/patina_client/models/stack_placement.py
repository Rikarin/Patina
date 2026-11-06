from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset







T = TypeVar("T", bound="StackPlacement")



@_attrs_define
class StackPlacement:
    """ Where a fill sits on the surface, in whichever projection it uses.

        Attributes:
            scale (float): How many times the picture repeats. Above zero. Default: 1.0.
            rotation (float): How far it turns, in radians. Default: 0.0.
            offset_x (float): How far it moves along its first axis, in repeats. Default: 0.0.
            offset_y (float): How far it moves along its second axis, in repeats. Default: 0.0.
            sharpness (float): How hard the seam between a tri-planar's planes is. Default: 4.0.
     """

    scale: float = 1.0
    rotation: float = 0.0
    offset_x: float = 0.0
    offset_y: float = 0.0
    sharpness: float = 4.0
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        scale = self.scale

        rotation = self.rotation

        offset_x = self.offset_x

        offset_y = self.offset_y

        sharpness = self.sharpness


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "scale": scale,
            "rotation": rotation,
            "offsetX": offset_x,
            "offsetY": offset_y,
            "sharpness": sharpness,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        scale = d.pop("scale")

        rotation = d.pop("rotation")

        offset_x = d.pop("offsetX")

        offset_y = d.pop("offsetY")

        sharpness = d.pop("sharpness")

        stack_placement = cls(
            scale=scale,
            rotation=rotation,
            offset_x=offset_x,
            offset_y=offset_y,
            sharpness=sharpness,
        )


        stack_placement.additional_properties = d
        return stack_placement

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
