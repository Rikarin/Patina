from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..models.graph_command_graph_arrange_op import GraphCommandGraphArrangeOp
from typing import cast






T = TypeVar("T", bound="GraphCommandGraphArrange")



@_attrs_define
class GraphCommandGraphArrange:
    """ Lays nodes out in columns, left to right in the order data flows, with Vixen's layout.

        Attributes:
            op (GraphCommandGraphArrangeOp):
            nodes (list[int]): The nodes, which keep their top-left corner as a block and are laid out among themselves;
                empty for the whole
                graph.
     """

    op: GraphCommandGraphArrangeOp
    nodes: list[int]
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        op = self.op.value

        nodes = self.nodes




        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "op": op,
            "nodes": nodes,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        op = GraphCommandGraphArrangeOp(d.pop("op"))




        nodes = cast(list[int], d.pop("nodes"))


        graph_command_graph_arrange = cls(
            op=op,
            nodes=nodes,
        )


        graph_command_graph_arrange.additional_properties = d
        return graph_command_graph_arrange

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
