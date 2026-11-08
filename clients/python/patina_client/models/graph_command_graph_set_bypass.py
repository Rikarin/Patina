from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..models.graph_command_graph_set_bypass_op import GraphCommandGraphSetBypassOp
from typing import cast






T = TypeVar("T", bound="GraphCommandGraphSetBypass")



@_attrs_define
class GraphCommandGraphSetBypass:
    """ Switches nodes off or back on. A switched-off node keeps its wires and values; see GraphNodeState.

        Attributes:
            op (GraphCommandGraphSetBypassOp):
            nodes (list[int]):
            bypassed (bool):
     """

    op: GraphCommandGraphSetBypassOp
    nodes: list[int]
    bypassed: bool
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        op = self.op.value

        nodes = self.nodes



        bypassed = self.bypassed


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "op": op,
            "nodes": nodes,
            "bypassed": bypassed,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        op = GraphCommandGraphSetBypassOp(d.pop("op"))




        nodes = cast(list[int], d.pop("nodes"))


        bypassed = d.pop("bypassed")

        graph_command_graph_set_bypass = cls(
            op=op,
            nodes=nodes,
            bypassed=bypassed,
        )


        graph_command_graph_set_bypass.additional_properties = d
        return graph_command_graph_set_bypass

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
