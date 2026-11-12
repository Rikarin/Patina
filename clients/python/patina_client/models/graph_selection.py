from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast






T = TypeVar("T", bound="GraphSelection")



@_attrs_define
class GraphSelection:
    """ The nodes chosen in a graph session, and the one kept on view whatever is chosen. A session has one selection,
    whichever client made it last, so a client that follows it, such as the viewport, needs no editor (#244).

        Attributes:
            nodes (list[int]): The chosen nodes, the first being the one a single-node view shows.
            pinned (int | None | Unset): The node the views keep showing while the choice moves on, or null.
     """

    nodes: list[int]
    pinned: int | None | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        nodes = self.nodes



        pinned: int | None | Unset
        if isinstance(self.pinned, Unset):
            pinned = UNSET
        else:
            pinned = self.pinned


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "nodes": nodes,
        })
        if pinned is not UNSET:
            field_dict["pinned"] = pinned

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        nodes = cast(list[int], d.pop("nodes"))


        def _parse_pinned(data: object) -> int | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(int | None | Unset, data)

        pinned = _parse_pinned(d.pop("pinned", UNSET))


        graph_selection = cls(
            nodes=nodes,
            pinned=pinned,
        )


        graph_selection.additional_properties = d
        return graph_selection

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
