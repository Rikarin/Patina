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
  from ..models.viewport_display import ViewportDisplay
  from ..models.viewport_follow import ViewportFollow
  from ..models.viewport_highlight import ViewportHighlight
  from ..models.viewport_isolation import ViewportIsolation
  from ..models.viewport_map_paths import ViewportMapPaths
  from ..models.viewport_node_shown import ViewportNodeShown





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
            camera (ViewportCameraState): The viewport's camera: an orbit around the model. Its lens is in ViewportDisplay.
            width (int): The size clients asked for, in pixels.
            height (int): The size clients asked for, in pixels.
            render_scale (float): The share of that size frames are rendered at, which drops while a client cannot keep up.
            frames (int): How many frames this Server has published.
            display (ViewportDisplay): How the viewport is lit, shown and finished. None of it reaches a file that is
                exported; a project keeps it, so
                it opens as it was left. Angles are radians and exposure is in stops; an effect at zero is off. A field left out
                of a request is the one the viewport opens with.
            mesh (None | str | Unset): The mesh's path, or null when none is open.
            error (None | str | Unset): Why the last command failed, or null.
            stack (None | str | Unset): The stack session it is wearing, or null.
            isolation (None | Unset | ViewportIsolation):
            node (None | Unset | ViewportNodeShown):
            following (None | Unset | ViewportFollow):
            highlight (None | Unset | ViewportHighlight):
     """

    maps: ViewportMapPaths
    camera: ViewportCameraState
    width: int
    height: int
    render_scale: float
    frames: int
    display: ViewportDisplay
    mesh: None | str | Unset = UNSET
    error: None | str | Unset = UNSET
    stack: None | str | Unset = UNSET
    isolation: None | Unset | ViewportIsolation = UNSET
    node: None | Unset | ViewportNodeShown = UNSET
    following: None | Unset | ViewportFollow = UNSET
    highlight: None | Unset | ViewportHighlight = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.viewport_camera_state import ViewportCameraState # noqa: PLC0415
        from ..models.viewport_display import ViewportDisplay # noqa: PLC0415
        from ..models.viewport_follow import ViewportFollow # noqa: PLC0415
        from ..models.viewport_highlight import ViewportHighlight # noqa: PLC0415
        from ..models.viewport_isolation import ViewportIsolation # noqa: PLC0415
        from ..models.viewport_map_paths import ViewportMapPaths # noqa: PLC0415
        from ..models.viewport_node_shown import ViewportNodeShown # noqa: PLC0415
        maps = self.maps.to_dict()

        camera = self.camera.to_dict()

        width = self.width

        height = self.height

        render_scale = self.render_scale

        frames = self.frames

        display = self.display.to_dict()

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

        isolation: dict[str, Any] | None | Unset
        if isinstance(self.isolation, Unset):
            isolation = UNSET
        elif isinstance(self.isolation, ViewportIsolation):
            isolation = self.isolation.to_dict()
        else:
            isolation = self.isolation

        node: dict[str, Any] | None | Unset
        if isinstance(self.node, Unset):
            node = UNSET
        elif isinstance(self.node, ViewportNodeShown):
            node = self.node.to_dict()
        else:
            node = self.node

        following: dict[str, Any] | None | Unset
        if isinstance(self.following, Unset):
            following = UNSET
        elif isinstance(self.following, ViewportFollow):
            following = self.following.to_dict()
        else:
            following = self.following

        highlight: dict[str, Any] | None | Unset
        if isinstance(self.highlight, Unset):
            highlight = UNSET
        elif isinstance(self.highlight, ViewportHighlight):
            highlight = self.highlight.to_dict()
        else:
            highlight = self.highlight


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "maps": maps,
            "camera": camera,
            "width": width,
            "height": height,
            "renderScale": render_scale,
            "frames": frames,
            "display": display,
        })
        if mesh is not UNSET:
            field_dict["mesh"] = mesh
        if error is not UNSET:
            field_dict["error"] = error
        if stack is not UNSET:
            field_dict["stack"] = stack
        if isolation is not UNSET:
            field_dict["isolation"] = isolation
        if node is not UNSET:
            field_dict["node"] = node
        if following is not UNSET:
            field_dict["following"] = following
        if highlight is not UNSET:
            field_dict["highlight"] = highlight

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.viewport_camera_state import ViewportCameraState # noqa: PLC0415
        from ..models.viewport_display import ViewportDisplay # noqa: PLC0415
        from ..models.viewport_follow import ViewportFollow # noqa: PLC0415
        from ..models.viewport_highlight import ViewportHighlight # noqa: PLC0415
        from ..models.viewport_isolation import ViewportIsolation # noqa: PLC0415
        from ..models.viewport_map_paths import ViewportMapPaths # noqa: PLC0415
        from ..models.viewport_node_shown import ViewportNodeShown # noqa: PLC0415
        d = dict(src_dict)
        maps = ViewportMapPaths.from_dict(d.pop("maps"))




        camera = ViewportCameraState.from_dict(d.pop("camera"))




        width = d.pop("width")

        height = d.pop("height")

        render_scale = d.pop("renderScale")

        frames = d.pop("frames")

        display = ViewportDisplay.from_dict(d.pop("display"))




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


        def _parse_isolation(data: object) -> None | Unset | ViewportIsolation:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                isolation_type_1 = ViewportIsolation.from_dict(data)



                return isolation_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(None | Unset | ViewportIsolation, data)

        isolation = _parse_isolation(d.pop("isolation", UNSET))


        def _parse_node(data: object) -> None | Unset | ViewportNodeShown:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                node_type_1 = ViewportNodeShown.from_dict(data)



                return node_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(None | Unset | ViewportNodeShown, data)

        node = _parse_node(d.pop("node", UNSET))


        def _parse_following(data: object) -> None | Unset | ViewportFollow:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                following_type_1 = ViewportFollow.from_dict(data)



                return following_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(None | Unset | ViewportFollow, data)

        following = _parse_following(d.pop("following", UNSET))


        def _parse_highlight(data: object) -> None | Unset | ViewportHighlight:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                highlight_type_1 = ViewportHighlight.from_dict(data)



                return highlight_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(None | Unset | ViewportHighlight, data)

        highlight = _parse_highlight(d.pop("highlight", UNSET))


        viewport_state = cls(
            maps=maps,
            camera=camera,
            width=width,
            height=height,
            render_scale=render_scale,
            frames=frames,
            display=display,
            mesh=mesh,
            error=error,
            stack=stack,
            isolation=isolation,
            node=node,
            following=following,
            highlight=highlight,
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
