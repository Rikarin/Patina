from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast






T = TypeVar("T", bound="ViewportFollow")



@_attrs_define
class ViewportFollow:
    """ Keeps a graph session on the viewport, shown again after each of its evaluations, until something else is laid
    on the mesh. The Server does the following, so a client that asks needs to do nothing more (#244).

        Attributes:
            node (bool): Show the node the session's selection views, its pinned node or else its first chosen one, alone in
                place of the
                material, as `PUT /viewport/graph` with a node does; its Output nodes when it views none or the node has no
                picture. False shows the Output nodes. Default: False.
            graph (None | str | Unset): The graph's session, or null to stop following.
     """

    node: bool = False
    graph: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        node = self.node

        graph: None | str | Unset
        if isinstance(self.graph, Unset):
            graph = UNSET
        else:
            graph = self.graph


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "node": node,
        })
        if graph is not UNSET:
            field_dict["graph"] = graph

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        node = d.pop("node")

        def _parse_graph(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        graph = _parse_graph(d.pop("graph", UNSET))


        viewport_follow = cls(
            node=node,
            graph=graph,
        )


        viewport_follow.additional_properties = d
        return viewport_follow

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
