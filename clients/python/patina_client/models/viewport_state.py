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
  from ..models.viewport_map_paths import ViewportMapPaths





T = TypeVar("T", bound="ViewportState")



@_attrs_define
class ViewportState:
    """ The reply to `GET /api/v1/viewport`. The Server owns all of this; a Worker can die without losing it.

        Attributes:
            maps (ViewportMapPaths): Pictures to lay over the mesh's own materials, on every part. Each is a path on the
                Server's machine, and a slot
                left null keeps what the material asks for. A filled slot replaces the material's factor too, so what shows
                is the picture. Roughness is read from green, metallic from blue and occlusion from red, so one packed file or
                three grey ones both work. PNG, JPEG and EXR are read, base colour as sRGB and the rest as linear.
            camera (ViewportCameraState): The viewport's camera: an orbit around the model.
            width (int): The size clients asked for, in pixels.
            height (int): The size clients asked for, in pixels.
            render_scale (float): The share of that size frames are rendered at, which drops while a client cannot keep up.
            frames (int): How many frames this Server has published.
            mesh (None | str | Unset): The mesh's path, or null when none is open.
            error (None | str | Unset): Why the last command failed, or null.
            stack (None | str | Unset): The stack session it is wearing, or null.
     """

    maps: ViewportMapPaths
    camera: ViewportCameraState
    width: int
    height: int
    render_scale: float
    frames: int
    mesh: None | str | Unset = UNSET
    error: None | str | Unset = UNSET
    stack: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.viewport_camera_state import ViewportCameraState # noqa: PLC0415
        from ..models.viewport_map_paths import ViewportMapPaths # noqa: PLC0415
        maps = self.maps.to_dict()

        camera = self.camera.to_dict()

        width = self.width

        height = self.height

        render_scale = self.render_scale

        frames = self.frames

        mesh: None | str | Unset
        if isinstance(self.mesh, Unset):
            mesh = UNSET
        else:
            mesh = self.mesh

        error: None | str | Unset
        if isinstance(self.error, Unset):
            error = UNSET
        else:
            error = self.error

        stack: None | str | Unset
        if isinstance(self.stack, Unset):
            stack = UNSET
        else:
            stack = self.stack


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "maps": maps,
            "camera": camera,
            "width": width,
            "height": height,
            "renderScale": render_scale,
            "frames": frames,
        })
        if mesh is not UNSET:
            field_dict["mesh"] = mesh
        if error is not UNSET:
            field_dict["error"] = error
        if stack is not UNSET:
            field_dict["stack"] = stack

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.viewport_camera_state import ViewportCameraState # noqa: PLC0415
        from ..models.viewport_map_paths import ViewportMapPaths # noqa: PLC0415
        d = dict(src_dict)
        maps = ViewportMapPaths.from_dict(d.pop("maps"))




        camera = ViewportCameraState.from_dict(d.pop("camera"))




        width = d.pop("width")

        height = d.pop("height")

        render_scale = d.pop("renderScale")

        frames = d.pop("frames")

        def _parse_mesh(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        mesh = _parse_mesh(d.pop("mesh", UNSET))


        def _parse_error(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        error = _parse_error(d.pop("error", UNSET))


        def _parse_stack(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        stack = _parse_stack(d.pop("stack", UNSET))


        viewport_state = cls(
            maps=maps,
            camera=camera,
            width=width,
            height=height,
            render_scale=render_scale,
            frames=frames,
            mesh=mesh,
            error=error,
            stack=stack,
        )


        viewport_state.additional_properties = d
        return viewport_state

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
