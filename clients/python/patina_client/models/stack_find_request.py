from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset







T = TypeVar("T", bound="StackFindRequest")



@_attrs_define
class StackFindRequest:
    """ Asks which layer is on top at a place on a stack's map, as a click on the mesh or the map finds it.

        Attributes:
            u (float): Across the map, 0 to 1: the texture coordinate `POST /viewport/pick` gives.
            v (float): Down it, 0 to 1.
     """

    u: float
    v: float
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        u = self.u

        v = self.v


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "u": u,
            "v": v,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        u = d.pop("u")

        v = d.pop("v")

        stack_find_request = cls(
            u=u,
            v=v,
        )


        stack_find_request.additional_properties = d
        return stack_find_request

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
