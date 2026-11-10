from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast






T = TypeVar("T", bound="StackSampledChannel")



@_attrs_define
class StackSampledChannel:
    """ What one channel holds at a place.

        Attributes:
            usage (str): The channel.
            value (list[float]): Red, green, blue and alpha, 0 to 1, as the map stores them and a stroke's colour is given:
                a one-number channel
                repeats its number in the first three.
     """

    usage: str
    value: list[float]
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        usage = self.usage

        value = self.value




        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "usage": usage,
            "value": value,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        usage = d.pop("usage")

        value = cast(list[float], d.pop("value"))


        stack_sampled_channel = cls(
            usage=usage,
            value=value,
        )


        stack_sampled_channel.additional_properties = d
        return stack_sampled_channel

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
