from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..models.graph_command_graph_set_group_op import GraphCommandGraphSetGroupOp
from ..types import UNSET, Unset
from typing import cast






T = TypeVar("T", bound="GraphCommandGraphSetGroup")



@_attrs_define
class GraphCommandGraphSetGroup:
    """ Changes a group: anything left null stays as it is.

        Attributes:
            op (GraphCommandGraphSetGroupOp):
            id (int): The group.
            title (None | str | Unset): Its new title.
            nodes (list[int] | None | Unset): Its new members.
            color (None | str | Unset): Its frame's CSS colour, or empty for the client's own choice.
            description (None | str | Unset): What its nodes do together.
     """

    op: GraphCommandGraphSetGroupOp
    id: int
    title: None | str | Unset = UNSET
    nodes: list[int] | None | Unset = UNSET
    color: None | str | Unset = UNSET
    description: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        op = self.op.value

        id = self.id

        title: None | str | Unset
        if isinstance(self.title, Unset):
            title = UNSET
        else:
            title = self.title

        nodes: list[int] | None | Unset
        if isinstance(self.nodes, Unset):
            nodes = UNSET
        elif isinstance(self.nodes, list):
            nodes = self.nodes


        else:
            nodes = self.nodes

        color: None | str | Unset
        if isinstance(self.color, Unset):
            color = UNSET
        else:
            color = self.color

        description: None | str | Unset
        if isinstance(self.description, Unset):
            description = UNSET
        else:
            description = self.description


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "op": op,
            "id": id,
        })
        if title is not UNSET:
            field_dict["title"] = title
        if nodes is not UNSET:
            field_dict["nodes"] = nodes
        if color is not UNSET:
            field_dict["color"] = color
        if description is not UNSET:
            field_dict["description"] = description

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        op = GraphCommandGraphSetGroupOp(d.pop("op"))




        id = d.pop("id")

        def _parse_title(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        title = _parse_title(d.pop("title", UNSET))


        def _parse_nodes(data: object) -> list[int] | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, list):
                    raise TypeError()
                nodes_type_1 = cast(list[int], data)

                return nodes_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(list[int] | None | Unset, data)

        nodes = _parse_nodes(d.pop("nodes", UNSET))


        def _parse_color(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        color = _parse_color(d.pop("color", UNSET))


        def _parse_description(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        description = _parse_description(d.pop("description", UNSET))


        graph_command_graph_set_group = cls(
            op=op,
            id=id,
            title=title,
            nodes=nodes,
            color=color,
            description=description,
        )


        graph_command_graph_set_group.additional_properties = d
        return graph_command_graph_set_group

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
