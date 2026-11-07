from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast






T = TypeVar("T", bound="GraphPortRange")



@_attrs_define
class GraphPortRange:
    """ What an input port says about its numbers. Any end may be open, as null.

        Attributes:
            minimum (float | None | Unset): The smallest number that means anything.
            maximum (float | None | Unset): The largest number that means anything.
            soft_minimum (float | None | Unset): Where a slider starts, when the port's meaning gives that. Numbers go on
                past it.
            soft_maximum (float | None | Unset): Where a slider ends, the same way.
     """

    minimum: float | None | Unset = UNSET
    maximum: float | None | Unset = UNSET
    soft_minimum: float | None | Unset = UNSET
    soft_maximum: float | None | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        minimum: float | None | Unset
        if isinstance(self.minimum, Unset):
            minimum = UNSET
        else:
            minimum = self.minimum

        maximum: float | None | Unset
        if isinstance(self.maximum, Unset):
            maximum = UNSET
        else:
            maximum = self.maximum

        soft_minimum: float | None | Unset
        if isinstance(self.soft_minimum, Unset):
            soft_minimum = UNSET
        else:
            soft_minimum = self.soft_minimum

        soft_maximum: float | None | Unset
        if isinstance(self.soft_maximum, Unset):
            soft_maximum = UNSET
        else:
            soft_maximum = self.soft_maximum


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
        })
        if minimum is not UNSET:
            field_dict["minimum"] = minimum
        if maximum is not UNSET:
            field_dict["maximum"] = maximum
        if soft_minimum is not UNSET:
            field_dict["softMinimum"] = soft_minimum
        if soft_maximum is not UNSET:
            field_dict["softMaximum"] = soft_maximum

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        def _parse_minimum(data: object) -> float | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(float | None | Unset, data)

        minimum = _parse_minimum(d.pop("minimum", UNSET))


        def _parse_maximum(data: object) -> float | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(float | None | Unset, data)

        maximum = _parse_maximum(d.pop("maximum", UNSET))


        def _parse_soft_minimum(data: object) -> float | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(float | None | Unset, data)

        soft_minimum = _parse_soft_minimum(d.pop("softMinimum", UNSET))


        def _parse_soft_maximum(data: object) -> float | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(float | None | Unset, data)

        soft_maximum = _parse_soft_maximum(d.pop("softMaximum", UNSET))


        graph_port_range = cls(
            minimum=minimum,
            maximum=maximum,
            soft_minimum=soft_minimum,
            soft_maximum=soft_maximum,
        )


        graph_port_range.additional_properties = d
        return graph_port_range

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
