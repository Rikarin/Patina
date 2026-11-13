from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast






T = TypeVar("T", bound="ViewportCastHit")



@_attrs_define
class ViewportCastHit:
    """ The first place one ray met the mesh, ahead of where it starts.

        Attributes:
            found (bool): Whether it met the mesh.
            position (list[float] | None | Unset): Where, in the mesh's own space: three numbers.
            normal (list[float] | None | Unset): The face it met, as a unit direction out of the side the ray came from:
                three numbers.
     """

    found: bool
    position: list[float] | None | Unset = UNSET
    normal: list[float] | None | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        found = self.found

        position: list[float] | None | Unset
        if isinstance(self.position, Unset):
            position = UNSET
        elif isinstance(self.position, list):
            position = self.position


        else:
            position = self.position

        normal: list[float] | None | Unset
        if isinstance(self.normal, Unset):
            normal = UNSET
        elif isinstance(self.normal, list):
            normal = self.normal


        else:
            normal = self.normal


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "found": found,
        })
        if position is not UNSET:
            field_dict["position"] = position
        if normal is not UNSET:
            field_dict["normal"] = normal

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        found = d.pop("found")

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


        viewport_cast_hit = cls(
            found=found,
            position=position,
            normal=normal,
        )


        viewport_cast_hit.additional_properties = d
        return viewport_cast_hit

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
