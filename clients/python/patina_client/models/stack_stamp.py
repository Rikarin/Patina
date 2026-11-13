from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast






T = TypeVar("T", bound="StackStamp")



@_attrs_define
class StackStamp:
    """ Where the brush goes: in texels of the image, or on the picture of a stroke with a view.

        Attributes:
            x (float): Its column.
            y (float): Its row.
            pressure (float): How hard, 0 to 1. Default: 1.0.
            tilt (float | None | Unset): How far a pen leans, 0 upright to 1 lying flat, or null for upright, as a mouse is.
            azimuth (float | None | Unset): Which way a pen leans, in radians on the picture as a brush's angle is measured:
                0 towards +x, a quarter turn
                towards +y. Null is 0.
            twist (float | None | Unset): How far a pen's barrel is turned, in radians, measured as a brush's angle is. Null
                is 0.
     """

    x: float
    y: float
    pressure: float = 1.0
    tilt: float | None | Unset = UNSET
    azimuth: float | None | Unset = UNSET
    twist: float | None | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        x = self.x

        y = self.y

        pressure = self.pressure

        tilt: float | None | Unset
        if isinstance(self.tilt, Unset):
            tilt = UNSET
        else:
            tilt = self.tilt

        azimuth: float | None | Unset
        if isinstance(self.azimuth, Unset):
            azimuth = UNSET
        else:
            azimuth = self.azimuth

        twist: float | None | Unset
        if isinstance(self.twist, Unset):
            twist = UNSET
        else:
            twist = self.twist


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "x": x,
            "y": y,
            "pressure": pressure,
        })
        if tilt is not UNSET:
            field_dict["tilt"] = tilt
        if azimuth is not UNSET:
            field_dict["azimuth"] = azimuth
        if twist is not UNSET:
            field_dict["twist"] = twist

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        x = d.pop("x")

        y = d.pop("y")

        pressure = d.pop("pressure")

        def _parse_tilt(data: object) -> float | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(float | None | Unset, data)

        tilt = _parse_tilt(d.pop("tilt", UNSET))


        def _parse_azimuth(data: object) -> float | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(float | None | Unset, data)

        azimuth = _parse_azimuth(d.pop("azimuth", UNSET))


        def _parse_twist(data: object) -> float | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(float | None | Unset, data)

        twist = _parse_twist(d.pop("twist", UNSET))


        stack_stamp = cls(
            x=x,
            y=y,
            pressure=pressure,
            tilt=tilt,
            azimuth=azimuth,
            twist=twist,
        )


        stack_stamp.additional_properties = d
        return stack_stamp

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
