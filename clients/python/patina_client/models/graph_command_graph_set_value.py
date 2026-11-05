from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..models.graph_command_graph_set_value_op import GraphCommandGraphSetValueOp
from typing import cast






T = TypeVar("T", bound="GraphCommandGraphSetValue")



@_attrs_define
class GraphCommandGraphSetValue:
    """ Sets the numbers on a node's input. This is what a slider sends.

        Attributes:
            op (GraphCommandGraphSetValueOp):
            node (int): The node.
            port (str): The input.
            value (list[float]): One number per lane of the port.
     """

    op: GraphCommandGraphSetValueOp
    node: int
    port: str
    value: list[float]
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        op = self.op.value

        node = self.node

        port = self.port

        value = self.value




        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "op": op,
            "node": node,
            "port": port,
            "value": value,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        op = GraphCommandGraphSetValueOp(d.pop("op"))




        node = d.pop("node")

        port = d.pop("port")

        value = cast(list[float], d.pop("value"))


        graph_command_graph_set_value = cls(
            op=op,
            node=node,
            port=port,
            value=value,
        )


        graph_command_graph_set_value.additional_properties = d
        return graph_command_graph_set_value

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
