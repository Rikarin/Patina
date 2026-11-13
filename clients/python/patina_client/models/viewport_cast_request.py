from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast






T = TypeVar("T", bound="ViewportCastRequest")



@_attrs_define
class ViewportCastRequest:
    """ Asks where rays along one direction meet the open mesh, in its own space and through what the viewport draws
    (#263): a decal's surface measured along the decal's own direction, wherever the camera is, in one round trip.

        Attributes:
            origins (list[float]): Where each ray starts: three numbers to a ray, at most int
                ViewportCastRequest.MaximumRays rays.
            direction (list[float]): The way every ray goes: three numbers, not all zero.
     """

    origins: list[float]
    direction: list[float]
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        origins = self.origins



        direction = self.direction




        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "origins": origins,
            "direction": direction,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        origins = cast(list[float], d.pop("origins"))


        direction = cast(list[float], d.pop("direction"))


        viewport_cast_request = cls(
            origins=origins,
            direction=direction,
        )


        viewport_cast_request.additional_properties = d
        return viewport_cast_request

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
