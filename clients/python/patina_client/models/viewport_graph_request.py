from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast






T = TypeVar("T", bound="ViewportGraphRequest")



@_attrs_define
class ViewportGraphRequest:
    """ Asks the viewport to show a graph on the mesh it is showing, or on the display's shape with none, as it stands
    now: its Output nodes as a material, or one node's picture alone.

        Attributes:
            graph (str): The graph's session.
            node (int | None | Unset): A node whose picture to show as stored, lit by nothing, in place of the material, or
                null for the Output nodes.
                The display's picture lane and scale apply to it, and its view, Lit or another, returns when the node is let go.
            port (None | str | Unset): The node's output, or null for the one its preview shows.
     """

    graph: str
    node: int | None | Unset = UNSET
    port: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        graph = self.graph

        node: int | None | Unset
        if isinstance(self.node, Unset):
            node = UNSET
        else:
            node = self.node

        port: None | str | Unset
        if isinstance(self.port, Unset):
            port = UNSET
        else:
            port = self.port


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "graph": graph,
        })
        if node is not UNSET:
            field_dict["node"] = node
        if port is not UNSET:
            field_dict["port"] = port

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        graph = d.pop("graph")

        def _parse_node(data: object) -> int | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(int | None | Unset, data)

        node = _parse_node(d.pop("node", UNSET))


        def _parse_port(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        port = _parse_port(d.pop("port", UNSET))


        viewport_graph_request = cls(
            graph=graph,
            node=node,
            port=port,
        )


        viewport_graph_request.additional_properties = d
        return viewport_graph_request

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
