from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..models.graph_command_graph_set_level_op import GraphCommandGraphSetLevelOp
from ..types import UNSET, Unset
from typing import cast






T = TypeVar("T", bound="GraphCommandGraphSetLevel")



@_attrs_define
class GraphCommandGraphSetLevel:
    """ Sets the size nodes are computed at, relative to their graph's; see GraphNodeState. A radius
    stays the same part of the picture at any level, because it is counted in the graph's base.

        Attributes:
            op (GraphCommandGraphSetLevelOp):
            nodes (list[int]): The nodes.
            level (int | None | Unset): From -2 (four times the graph's size) to 4 (a sixteenth of it), or null to follow
                the inputs.
     """

    op: GraphCommandGraphSetLevelOp
    nodes: list[int]
    level: int | None | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        op = self.op.value

        nodes = self.nodes



        level: int | None | Unset
        if isinstance(self.level, Unset):
            level = UNSET
        else:
            level = self.level


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "op": op,
            "nodes": nodes,
        })
        if level is not UNSET:
            field_dict["level"] = level

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        op = GraphCommandGraphSetLevelOp(d.pop("op"))




        nodes = cast(list[int], d.pop("nodes"))


        def _parse_level(data: object) -> int | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(int | None | Unset, data)

        level = _parse_level(d.pop("level", UNSET))


        graph_command_graph_set_level = cls(
            op=op,
            nodes=nodes,
            level=level,
        )


        graph_command_graph_set_level.additional_properties = d
        return graph_command_graph_set_level

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
