from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..models.graph_command_graph_add_port_op import GraphCommandGraphAddPortOp
from typing import cast

if TYPE_CHECKING:
  from ..models.graph_interface_port import GraphInterfacePort





T = TypeVar("T", bound="GraphCommandGraphAddPort")



@_attrs_define
class GraphCommandGraphAddPort:
    """ Gives the graph a new port. Its boundary node shows it at once, and a holding graph after a save.

        Attributes:
            op (GraphCommandGraphAddPortOp):
            port (GraphInterfacePort): One port a graph has when another graph holds it as a node.
     """

    op: GraphCommandGraphAddPortOp
    port: GraphInterfacePort
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.graph_interface_port import GraphInterfacePort # noqa: PLC0415
        op = self.op.value

        port = self.port.to_dict()


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "op": op,
            "port": port,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.graph_interface_port import GraphInterfacePort # noqa: PLC0415
        d = dict(src_dict)
        op = GraphCommandGraphAddPortOp(d.pop("op"))




        port = GraphInterfacePort.from_dict(d.pop("port"))




        graph_command_graph_add_port = cls(
            op=op,
            port=port,
        )


        graph_command_graph_add_port.additional_properties = d
        return graph_command_graph_add_port

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
