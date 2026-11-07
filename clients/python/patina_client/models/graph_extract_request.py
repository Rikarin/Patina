from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast






T = TypeVar("T", bound="GraphExtractRequest")



@_attrs_define
class GraphExtractRequest:
    """ Moves some nodes into a new compound beside the graph and puts a node holding it in their place.

        Attributes:
            name (str): Its node type's path, such as `Masks/Rust Edge`, which is also its file below `Compounds`.
            nodes (list[int]): The nodes. The wires that cross into them and out of them become its ports.
            x (float): Where the node holding it goes.
            y (float): Where the node holding it goes.
     """

    name: str
    nodes: list[int]
    x: float
    y: float
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        name = self.name

        nodes = self.nodes



        x = self.x

        y = self.y


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "name": name,
            "nodes": nodes,
            "x": x,
            "y": y,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        name = d.pop("name")

        nodes = cast(list[int], d.pop("nodes"))


        x = d.pop("x")

        y = d.pop("y")

        graph_extract_request = cls(
            name=name,
            nodes=nodes,
            x=x,
            y=y,
        )


        graph_extract_request.additional_properties = d
        return graph_extract_request

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
