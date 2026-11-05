from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..models.graph_command_graph_remove_port_op import GraphCommandGraphRemovePortOp






T = TypeVar("T", bound="GraphCommandGraphRemovePort")



@_attrs_define
class GraphCommandGraphRemovePort:
    """ Takes a port away, with the wires on its boundary node. Undoing it puts them back.

        Attributes:
            op (GraphCommandGraphRemovePortOp):
            name (str):
            direction (str):
     """

    op: GraphCommandGraphRemovePortOp
    name: str
    direction: str
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        op = self.op.value

        name = self.name

        direction = self.direction


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "op": op,
            "name": name,
            "direction": direction,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        op = GraphCommandGraphRemovePortOp(d.pop("op"))




        name = d.pop("name")

        direction = d.pop("direction")

        graph_command_graph_remove_port = cls(
            op=op,
            name=name,
            direction=direction,
        )


        graph_command_graph_remove_port.additional_properties = d
        return graph_command_graph_remove_port

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
