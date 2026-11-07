from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..models.graph_command_graph_add_group_op import GraphCommandGraphAddGroupOp
from ..types import UNSET, Unset
from typing import cast






T = TypeVar("T", bound="GraphCommandGraphAddGroup")



@_attrs_define
class GraphCommandGraphAddGroup:
    """ Draws a group frame round some nodes.

        Attributes:
            op (GraphCommandGraphAddGroupOp):
            title (str): What it is called.
            nodes (list[int]): Its members.
            ref (int | None | Unset): A negative number the client picks. The reply says which identity it became.
     """

    op: GraphCommandGraphAddGroupOp
    title: str
    nodes: list[int]
    ref: int | None | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        op = self.op.value

        title = self.title

        nodes = self.nodes



        ref: int | None | Unset
        if isinstance(self.ref, Unset):
            ref = UNSET
        else:
            ref = self.ref


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "op": op,
            "title": title,
            "nodes": nodes,
        })
        if ref is not UNSET:
            field_dict["ref"] = ref

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        op = GraphCommandGraphAddGroupOp(d.pop("op"))




        title = d.pop("title")

        nodes = cast(list[int], d.pop("nodes"))


        def _parse_ref(data: object) -> int | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(int | None | Unset, data)

        ref = _parse_ref(d.pop("ref", UNSET))


        graph_command_graph_add_group = cls(
            op=op,
            title=title,
            nodes=nodes,
            ref=ref,
        )


        graph_command_graph_add_group.additional_properties = d
        return graph_command_graph_add_group

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
