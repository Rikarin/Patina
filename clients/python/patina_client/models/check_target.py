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





T = TypeVar("T", bound="CheckTarget")



@_attrs_define
class CheckTarget:
    """ Where a problem is, for a client to go to it.

        Attributes:
            view (None | str | Unset): A viewport view that shows it, one of IReadOnlyList&lt;string&gt; ViewportViews.All,
                or null.
            camera (None | Unset | ViewportCameraState):
            u (float | None | Unset): A texture coordinate across where it is, or null.
            v (float | None | Unset): A texture coordinate down where it is, or null.
            setting (None | str | Unset): The project's setting that decides it, as `block.field` (`checks.memoryBudget`,
                `export.preset`), or null.
     """

    view: None | str | Unset = UNSET
    camera: None | Unset | ViewportCameraState = UNSET
    u: float | None | Unset = UNSET
    v: float | None | Unset = UNSET
    setting: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.viewport_camera_state import ViewportCameraState # noqa: PLC0415
        view: None | str | Unset
        if isinstance(self.view, Unset):
            view = UNSET
        else:
            view = self.view

        camera: dict[str, Any] | None | Unset
        if isinstance(self.camera, Unset):
            camera = UNSET
        elif isinstance(self.camera, ViewportCameraState):
            camera = self.camera.to_dict()
        else:
            camera = self.camera

        u: float | None | Unset
        if isinstance(self.u, Unset):
            u = UNSET
        else:
            u = self.u

        v: float | None | Unset
        if isinstance(self.v, Unset):
            v = UNSET
        else:
            v = self.v

        setting: None | str | Unset
        if isinstance(self.setting, Unset):
            setting = UNSET
        else:
            setting = self.setting


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
        })
        if view is not UNSET:
            field_dict["view"] = view
        if camera is not UNSET:
            field_dict["camera"] = camera
        if u is not UNSET:
            field_dict["u"] = u
        if v is not UNSET:
            field_dict["v"] = v
        if setting is not UNSET:
            field_dict["setting"] = setting

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.viewport_camera_state import ViewportCameraState # noqa: PLC0415
        d = dict(src_dict)
        def _parse_view(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        view = _parse_view(d.pop("view", UNSET))


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


        def _parse_u(data: object) -> float | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(float | None | Unset, data)

        u = _parse_u(d.pop("u", UNSET))


        def _parse_v(data: object) -> float | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(float | None | Unset, data)

        v = _parse_v(d.pop("v", UNSET))


        def _parse_setting(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        setting = _parse_setting(d.pop("setting", UNSET))


        check_target = cls(
            view=view,
            camera=camera,
            u=u,
            v=v,
            setting=setting,
        )


        check_target.additional_properties = d
        return check_target

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
