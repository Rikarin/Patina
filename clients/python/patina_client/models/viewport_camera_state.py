from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset







T = TypeVar("T", bound="ViewportCameraState")



@_attrs_define
class ViewportCameraState:
    """ The viewport's camera: an orbit around the model. Its lens is in ViewportDisplay.

        Attributes:
            yaw (float): Radians around the up axis.
            pitch (float): Radians above the horizon.
            distance (float): From the point it circles, in framings: at one, the sphere round the model's bounds just fits
                the picture's
                narrower side, whatever its shape and the field of view. double ViewportCameraState.Framed shows the whole
                model.
            pan_x (float): How far that point is off the model's centre along x, in bounding radii. Default: 0.0.
            pan_y (float): The same along y. Default: 0.0.
            pan_z (float): The same along z. Default: 0.0.
     """

    yaw: float
    pitch: float
    distance: float
    pan_x: float = 0.0
    pan_y: float = 0.0
    pan_z: float = 0.0
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        yaw = self.yaw

        pitch = self.pitch

        distance = self.distance

        pan_x = self.pan_x

        pan_y = self.pan_y

        pan_z = self.pan_z


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "yaw": yaw,
            "pitch": pitch,
            "distance": distance,
            "panX": pan_x,
            "panY": pan_y,
            "panZ": pan_z,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        yaw = d.pop("yaw")

        pitch = d.pop("pitch")

        distance = d.pop("distance")

        pan_x = d.pop("panX")

        pan_y = d.pop("panY")

        pan_z = d.pop("panZ")

        viewport_camera_state = cls(
            yaw=yaw,
            pitch=pitch,
            distance=distance,
            pan_x=pan_x,
            pan_y=pan_y,
            pan_z=pan_z,
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
