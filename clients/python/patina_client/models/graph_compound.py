from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset







T = TypeVar("T", bound="GraphCompound")



@_attrs_define
class GraphCompound:
    """ A compound a graph can hold, and the graph behind it.

        Attributes:
            type_ (str): Its node type's path, as `/node-types` lists it.
            path (str): What `POST /graphs` takes to open it.
            read_only (bool): True for a compound that ships with Patina, which opens to be read.
     """

    type_: str
    path: str
    read_only: bool
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        type_ = self.type_

        path = self.path

        read_only = self.read_only


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "type": type_,
            "path": path,
            "readOnly": read_only,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        type_ = d.pop("type")

        path = d.pop("path")

        read_only = d.pop("readOnly")

        graph_compound = cls(
            type_=type_,
            path=path,
            read_only=read_only,
        )


        graph_compound.additional_properties = d
        return graph_compound

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
