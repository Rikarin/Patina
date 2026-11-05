from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset







T = TypeVar("T", bound="ViewportCameraState")



@_attrs_define
class ViewportCameraState:
    """ The viewport's camera: an orbit around the model.

        Attributes:
            yaw (float): Radians around the up axis.
            pitch (float): Radians above the horizon.
            distance (float): From the model's centre, in bounding radii.
     """

    yaw: float
    pitch: float
    distance: float
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        yaw = self.yaw

        pitch = self.pitch

        distance = self.distance


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "yaw": yaw,
            "pitch": pitch,
            "distance": distance,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        yaw = d.pop("yaw")

        pitch = d.pop("pitch")

        distance = d.pop("distance")

        viewport_camera_state = cls(
            yaw=yaw,
            pitch=pitch,
            distance=distance,
        )


        viewport_camera_state.additional_properties = d
        return viewport_camera_state

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
