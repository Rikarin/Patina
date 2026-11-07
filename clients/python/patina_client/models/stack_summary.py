from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset







T = TypeVar("T", bound="StackSummary")



@_attrs_define
class StackSummary:
    """ An open stack, for a list.

        Attributes:
            id (str): The session.
            path (str): Its file.
            name (str): What it is called.
            revision (int): The revision of the document.
            dirty (bool): Whether it has edits no file holds.
     """

    id: str
    path: str
    name: str
    revision: int
    dirty: bool
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        id = self.id

        path = self.path

        name = self.name

        revision = self.revision

        dirty = self.dirty


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "id": id,
            "path": path,
            "name": name,
            "revision": revision,
            "dirty": dirty,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        id = d.pop("id")

        path = d.pop("path")

        name = d.pop("name")

        revision = d.pop("revision")

        dirty = d.pop("dirty")

        stack_summary = cls(
            id=id,
            path=path,
            name=name,
            revision=revision,
            dirty=dirty,
        )


        stack_summary.additional_properties = d
        return stack_summary

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
