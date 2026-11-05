from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..models.graph_command_graph_add_node_op import GraphCommandGraphAddNodeOp
from ..types import UNSET, Unset
from typing import cast






T = TypeVar("T", bound="GraphCommandGraphAddNode")



@_attrs_define
class GraphCommandGraphAddNode:
    """ Adds a node.

        Attributes:
            op (GraphCommandGraphAddNodeOp):
            type_ (str): The node type's path.
            x (float): Where it goes.
            y (float): Where it goes.
            ref (int | None | Unset): A negative number the client picks. Later commands in this batch may use it wherever
                they name a node, and the
                reply says which identity it became.
     """

    op: GraphCommandGraphAddNodeOp
    type_: str
    x: float
    y: float
    ref: int | None | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        op = self.op.value

        type_ = self.type_

        x = self.x

        y = self.y

        ref: int | None | Unset
        if isinstance(self.ref, Unset):
            ref = UNSET
        else:
            ref = self.ref


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "op": op,
            "type": type_,
            "x": x,
            "y": y,
        })
        if ref is not UNSET:
            field_dict["ref"] = ref

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        op = GraphCommandGraphAddNodeOp(d.pop("op"))




        type_ = d.pop("type")

        x = d.pop("x")

        y = d.pop("y")

        def _parse_ref(data: object) -> int | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(int | None | Unset, data)

        ref = _parse_ref(d.pop("ref", UNSET))


        graph_command_graph_add_node = cls(
            op=op,
            type_=type_,
            x=x,
            y=y,
            ref=ref,
        )


        graph_command_graph_add_node.additional_properties = d
        return graph_command_graph_add_node

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
