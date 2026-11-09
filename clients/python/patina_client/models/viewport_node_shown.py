from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset







T = TypeVar("T", bound="ViewportNodeShown")



@_attrs_define
class ViewportNodeShown:
    """ A graph node whose picture the viewport shows in place of a material.

        Attributes:
            graph (str): The graph's session.
            node (int): The node.
            port (str): Its output.
     """

    graph: str
    node: int
    port: str
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        graph = self.graph

        node = self.node

        port = self.port


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "graph": graph,
            "node": node,
            "port": port,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        graph = d.pop("graph")

        node = d.pop("node")

        port = d.pop("port")

        viewport_node_shown = cls(
            graph=graph,
            node=node,
            port=port,
        )


        viewport_node_shown.additional_properties = d
        return viewport_node_shown

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
