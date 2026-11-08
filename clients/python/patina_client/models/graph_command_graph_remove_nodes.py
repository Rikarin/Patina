from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..models.graph_command_graph_remove_nodes_op import GraphCommandGraphRemoveNodesOp
from typing import cast






T = TypeVar("T", bound="GraphCommandGraphRemoveNodes")



@_attrs_define
class GraphCommandGraphRemoveNodes:
    """ Removes nodes, and the wires that touch them.

        Attributes:
            op (GraphCommandGraphRemoveNodesOp):
            nodes (list[int]): The nodes.
            relink (bool): True to keep the chain joined: each input a removed node fed is wired to what fed that node's
                first input of
                the same kind, the way a bypassed node passes it on. Default: False.
     """

    op: GraphCommandGraphRemoveNodesOp
    nodes: list[int]
    relink: bool = False
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        op = self.op.value

        nodes = self.nodes



        relink = self.relink


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "op": op,
            "nodes": nodes,
            "relink": relink,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        op = GraphCommandGraphRemoveNodesOp(d.pop("op"))




        nodes = cast(list[int], d.pop("nodes"))


        relink = d.pop("relink")

        graph_command_graph_remove_nodes = cls(
            op=op,
            nodes=nodes,
            relink=relink,
        )


        graph_command_graph_remove_nodes.additional_properties = d
        return graph_command_graph_remove_nodes

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
