from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset







T = TypeVar("T", bound="FolderCost")



@_attrs_define
class FolderCost:
    """ What the documents below one folder or project cost (#207).

        Attributes:
            name (str): The folder or project, by its name in the folder asked about.
            documents (int): How many stacks are below it, at any depth.
            bytes_ (int): What their maps hold together, uncompressed at each document's size.
     """

    name: str
    documents: int
    bytes_: int
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        name = self.name

        documents = self.documents

        bytes_ = self.bytes_


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "name": name,
            "documents": documents,
            "bytes": bytes_,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        name = d.pop("name")

        documents = d.pop("documents")

        bytes_ = d.pop("bytes")

        folder_cost = cls(
            name=name,
            documents=documents,
            bytes_=bytes_,
        )


        folder_cost.additional_properties = d
        return folder_cost

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
