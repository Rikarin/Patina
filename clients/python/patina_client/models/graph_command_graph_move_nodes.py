from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..models.graph_command_graph_move_nodes_op import GraphCommandGraphMoveNodesOp
from typing import cast

if TYPE_CHECKING:
  from ..models.graph_node_position import GraphNodePosition





T = TypeVar("T", bound="GraphCommandGraphMoveNodes")



@_attrs_define
class GraphCommandGraphMoveNodes:
    """ Moves nodes. A move never evaluates anything.

        Attributes:
            op (GraphCommandGraphMoveNodesOp):
            positions (list[GraphNodePosition]):
     """

    op: GraphCommandGraphMoveNodesOp
    positions: list[GraphNodePosition]
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.graph_node_position import GraphNodePosition # noqa: PLC0415
        op = self.op.value

        positions = []
        for positions_item_data in self.positions:
            positions_item = positions_item_data.to_dict()
            positions.append(positions_item)




        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "op": op,
            "positions": positions,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.graph_node_position import GraphNodePosition # noqa: PLC0415
        d = dict(src_dict)
        op = GraphCommandGraphMoveNodesOp(d.pop("op"))




        positions = []
        _positions = d.pop("positions")
        for positions_item_data in (_positions):
            positions_item = GraphNodePosition.from_dict(positions_item_data)



            positions.append(positions_item)


        graph_command_graph_move_nodes = cls(
            op=op,
            positions=positions,
        )


        graph_command_graph_move_nodes.additional_properties = d
        return graph_command_graph_move_nodes

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
