from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..models.graph_command_graph_wire_op import GraphCommandGraphWireOp
from ..types import UNSET, Unset
from typing import cast






T = TypeVar("T", bound="GraphCommandGraphWire")



@_attrs_define
class GraphCommandGraphWire:
    """ Wires two nodes through the first ports that fit, for a node just made from a selection or a dropped wire, whose
    client cannot know its kinds. Nothing happens when none fit: a generator takes no input, and it is still wanted.

        Attributes:
            op (GraphCommandGraphWireOp):
            from_node (int): The node the wire leaves.
            to_node (int): The node the wire reaches.
            from_port (None | str | Unset): The output, or null for the first that fits.
            to_port (None | str | Unset): The input, or null for the first that fits.
     """

    op: GraphCommandGraphWireOp
    from_node: int
    to_node: int
    from_port: None | str | Unset = UNSET
    to_port: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        op = self.op.value

        from_node = self.from_node

        to_node = self.to_node

        from_port: None | str | Unset
        if isinstance(self.from_port, Unset):
            from_port = UNSET
        else:
            from_port = self.from_port

        to_port: None | str | Unset
        if isinstance(self.to_port, Unset):
            to_port = UNSET
        else:
            to_port = self.to_port


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "op": op,
            "fromNode": from_node,
            "toNode": to_node,
        })
        if from_port is not UNSET:
            field_dict["fromPort"] = from_port
        if to_port is not UNSET:
            field_dict["toPort"] = to_port

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        op = GraphCommandGraphWireOp(d.pop("op"))




        from_node = d.pop("fromNode")

        to_node = d.pop("toNode")

        def _parse_from_port(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        from_port = _parse_from_port(d.pop("fromPort", UNSET))


        def _parse_to_port(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        to_port = _parse_to_port(d.pop("toPort", UNSET))


        graph_command_graph_wire = cls(
            op=op,
            from_node=from_node,
            to_node=to_node,
            from_port=from_port,
            to_port=to_port,
        )


        graph_command_graph_wire.additional_properties = d
        return graph_command_graph_wire

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
