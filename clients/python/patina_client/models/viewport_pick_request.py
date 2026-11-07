from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast

if TYPE_CHECKING:
  from ..models.viewport_camera_state import ViewportCameraState





T = TypeVar("T", bound="ViewportPickRequest")



@_attrs_define
class ViewportPickRequest:
    """ Asks what is under a position in the viewport.

        Attributes:
            x (float): Across the picture, 0 to 1.
            y (float): Down it, 0 to 1.
            camera (None | Unset | ViewportCameraState):
            width (int | None | Unset): The picture's width in the units a stroke will use, or null for the viewport's.
            height (int | None | Unset): Its height.
     """

    x: float
    y: float
    camera: None | Unset | ViewportCameraState = UNSET
    width: int | None | Unset = UNSET
    height: int | None | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.viewport_camera_state import ViewportCameraState # noqa: PLC0415
        x = self.x

        y = self.y

        camera: dict[str, Any] | None | Unset
        if isinstance(self.camera, Unset):
            camera = UNSET
        elif isinstance(self.camera, ViewportCameraState):
            camera = self.camera.to_dict()
        else:
            camera = self.camera

        width: int | None | Unset
        if isinstance(self.width, Unset):
            width = UNSET
        else:
            width = self.width

        height: int | None | Unset
        if isinstance(self.height, Unset):
            height = UNSET
        else:
            height = self.height


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "x": x,
            "y": y,
        })
        if camera is not UNSET:
            field_dict["camera"] = camera
        if width is not UNSET:
            field_dict["width"] = width
        if height is not UNSET:
            field_dict["height"] = height

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.viewport_camera_state import ViewportCameraState # noqa: PLC0415
        d = dict(src_dict)
        x = d.pop("x")

        y = d.pop("y")

        def _parse_camera(data: object) -> None | Unset | ViewportCameraState:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                camera_type_1 = ViewportCameraState.from_dict(data)



                return camera_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(None | Unset | ViewportCameraState, data)

        camera = _parse_camera(d.pop("camera", UNSET))


        def _parse_width(data: object) -> int | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(int | None | Unset, data)

        width = _parse_width(d.pop("width", UNSET))


        def _parse_height(data: object) -> int | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(int | None | Unset, data)

        height = _parse_height(d.pop("height", UNSET))


        viewport_pick_request = cls(
            x=x,
            y=y,
            camera=camera,
            width=width,
            height=height,
        )


        viewport_pick_request.additional_properties = d
        return viewport_pick_request

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
