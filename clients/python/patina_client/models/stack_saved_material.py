from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset







T = TypeVar("T", bound="StackSavedMaterial")



@_attrs_define
class StackSavedMaterial:
    """ What a save made.

        Attributes:
            path (str): The document, below the library, as the shelf names it.
            role (str): `material` or `mask`, as the shelf files it.
            status (str): What came and what did not: painted pixels stay with the model they were painted on, and a
                reference to an
                anchor point that did not come reads nothing until a layer publishes that name where it is used.
     """

    path: str
    role: str
    status: str
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        path = self.path

        role = self.role

        status = self.status


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "path": path,
            "role": role,
            "status": status,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        path = d.pop("path")

        role = d.pop("role")

        status = d.pop("status")

        stack_saved_material = cls(
            path=path,
            role=role,
            status=status,
        )


        stack_saved_material.additional_properties = d
        return stack_saved_material

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
