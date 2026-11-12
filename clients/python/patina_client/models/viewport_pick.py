from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast

if TYPE_CHECKING:
  from ..models.stack_view import StackView





T = TypeVar("T", bound="ViewportPick")



@_attrs_define
class ViewportPick:
    """ What a position in the viewport landed on.

        Attributes:
            found (bool): Whether it met the mesh.
            view (StackView): A camera in the mesh's own space, which a stroke keeps because a position on a picture means
                nothing without
                it. Any client with a camera can fill this in; `POST /viewport/pick` gives the viewport's.
            triangle (int): The triangle. Default: -1.
            u (float): The texture coordinate across. Default: 0.0.
            v (float): The texture coordinate down. Default: 0.0.
            position (list[float] | None | Unset): Where on the mesh, in its own space: three numbers.
            bounds (list[float] | None | Unset): The box the mesh fits inside, in its own space: the least corner's three
                numbers, then the greatest's. A
                symmetry's model centre is its middle, and a client draws a symmetry's plane across it.
            normal (list[float] | None | Unset): The face it met, as a unit direction out of the side the camera sees: three
                numbers. A decal placed there faces
                it, and a path drawn through it keeps to faces that turn the same way.
            part (None | str | Unset): The named mesh it met, as the model names it, which a layer kept to some meshes takes
                or leaves; null when the
                model names none.
     """

    found: bool
    view: StackView
    triangle: int = -1
    u: float = 0.0
    v: float = 0.0
    position: list[float] | None | Unset = UNSET
    bounds: list[float] | None | Unset = UNSET
    normal: list[float] | None | Unset = UNSET
    part: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.stack_view import StackView # noqa: PLC0415
        found = self.found

        view = self.view.to_dict()

        triangle = self.triangle

        u = self.u

        v = self.v

        position: list[float] | None | Unset
        if isinstance(self.position, Unset):
            position = UNSET
        elif isinstance(self.position, list):
            position = self.position


        else:
            position = self.position

        bounds: list[float] | None | Unset
        if isinstance(self.bounds, Unset):
            bounds = UNSET
        elif isinstance(self.bounds, list):
            bounds = self.bounds


        else:
            bounds = self.bounds

        normal: list[float] | None | Unset
        if isinstance(self.normal, Unset):
            normal = UNSET
        elif isinstance(self.normal, list):
            normal = self.normal


        else:
            normal = self.normal

        part: None | str | Unset
        if isinstance(self.part, Unset):
            part = UNSET
        else:
            part = self.part


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "found": found,
            "view": view,
            "triangle": triangle,
            "u": u,
            "v": v,
        })
        if position is not UNSET:
            field_dict["position"] = position
        if bounds is not UNSET:
            field_dict["bounds"] = bounds
        if normal is not UNSET:
            field_dict["normal"] = normal
        if part is not UNSET:
            field_dict["part"] = part

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.stack_view import StackView # noqa: PLC0415
        d = dict(src_dict)
        found = d.pop("found")

        view = StackView.from_dict(d.pop("view"))




        triangle = d.pop("triangle")

        u = d.pop("u")

        v = d.pop("v")

        def _parse_position(data: object) -> list[float] | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, list):
                    raise TypeError()
                position_type_1 = cast(list[float], data)

                return position_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(list[float] | None | Unset, data)

        position = _parse_position(d.pop("position", UNSET))


        def _parse_bounds(data: object) -> list[float] | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, list):
                    raise TypeError()
                bounds_type_1 = cast(list[float], data)

                return bounds_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(list[float] | None | Unset, data)

        bounds = _parse_bounds(d.pop("bounds", UNSET))


        def _parse_normal(data: object) -> list[float] | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, list):
                    raise TypeError()
                normal_type_1 = cast(list[float], data)

                return normal_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(list[float] | None | Unset, data)

        normal = _parse_normal(d.pop("normal", UNSET))


        def _parse_part(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        part = _parse_part(d.pop("part", UNSET))


        viewport_pick = cls(
            found=found,
            view=view,
            triangle=triangle,
            u=u,
            v=v,
            position=position,
            bounds=bounds,
            normal=normal,
            part=part,
        )


        viewport_pick.additional_properties = d
        return viewport_pick

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
