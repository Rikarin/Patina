from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset







T = TypeVar("T", bound="GraphEdgeState")



@_attrs_define
class GraphEdgeState:
    """ One wire, from an output to an input.

        Attributes:
            from_node (int):
            from_port (str):
            to_node (int):
            to_port (str):
     """

    from_node: int
    from_port: str
    to_node: int
    to_port: str
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from_node = self.from_node

        from_port = self.from_port

        to_node = self.to_node

        to_port = self.to_port


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "fromNode": from_node,
            "fromPort": from_port,
            "toNode": to_node,
            "toPort": to_port,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        from_node = d.pop("fromNode")

        from_port = d.pop("fromPort")

        to_node = d.pop("toNode")

        to_port = d.pop("toPort")

        graph_edge_state = cls(
            from_node=from_node,
            from_port=from_port,
            to_node=to_node,
            to_port=to_port,
        )


        graph_edge_state.additional_properties = d
        return graph_edge_state

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
