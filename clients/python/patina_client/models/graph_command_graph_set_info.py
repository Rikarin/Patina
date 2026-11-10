from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..models.graph_command_graph_set_info_op import GraphCommandGraphSetInfoOp
from ..types import UNSET, Unset
from typing import cast






T = TypeVar("T", bound="GraphCommandGraphSetInfo")



@_attrs_define
class GraphCommandGraphSetInfo:
    """ Changes what the graph says about itself: anything left null stays as it is.

        Attributes:
            op (GraphCommandGraphSetInfoOp):
            description (None | str | Unset): What it makes and how it is meant to be used.
            author (None | str | Unset): Who made it.
            tags (list[str] | None | Unset): Words to find it by. Blank and repeated words are dropped, ignoring case.
            physical_width (float | None | Unset): The width one tile stands for, in centimetres. Zero, with PhysicalHeight
                zero, says nothing.
            physical_height (float | None | Unset): Its height, the same way. The two are set together.
     """

    op: GraphCommandGraphSetInfoOp
    description: None | str | Unset = UNSET
    author: None | str | Unset = UNSET
    tags: list[str] | None | Unset = UNSET
    physical_width: float | None | Unset = UNSET
    physical_height: float | None | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        op = self.op.value

        description: None | str | Unset
        if isinstance(self.description, Unset):
            description = UNSET
        else:
            description = self.description

        author: None | str | Unset
        if isinstance(self.author, Unset):
            author = UNSET
        else:
            author = self.author

        tags: list[str] | None | Unset
        if isinstance(self.tags, Unset):
            tags = UNSET
        elif isinstance(self.tags, list):
            tags = self.tags


        else:
            tags = self.tags

        physical_width: float | None | Unset
        if isinstance(self.physical_width, Unset):
            physical_width = UNSET
        else:
            physical_width = self.physical_width

        physical_height: float | None | Unset
        if isinstance(self.physical_height, Unset):
            physical_height = UNSET
        else:
            physical_height = self.physical_height


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "op": op,
        })
        if description is not UNSET:
            field_dict["description"] = description
        if author is not UNSET:
            field_dict["author"] = author
        if tags is not UNSET:
            field_dict["tags"] = tags
        if physical_width is not UNSET:
            field_dict["physicalWidth"] = physical_width
        if physical_height is not UNSET:
            field_dict["physicalHeight"] = physical_height

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        op = GraphCommandGraphSetInfoOp(d.pop("op"))




        def _parse_description(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        description = _parse_description(d.pop("description", UNSET))


        def _parse_author(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        author = _parse_author(d.pop("author", UNSET))


        def _parse_tags(data: object) -> list[str] | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, list):
                    raise TypeError()
                tags_type_1 = cast(list[str], data)

                return tags_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(list[str] | None | Unset, data)

        tags = _parse_tags(d.pop("tags", UNSET))


        def _parse_physical_width(data: object) -> float | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(float | None | Unset, data)

        physical_width = _parse_physical_width(d.pop("physicalWidth", UNSET))


        def _parse_physical_height(data: object) -> float | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(float | None | Unset, data)

        physical_height = _parse_physical_height(d.pop("physicalHeight", UNSET))


        graph_command_graph_set_info = cls(
            op=op,
            description=description,
            author=author,
            tags=tags,
            physical_width=physical_width,
            physical_height=physical_height,
        )


        graph_command_graph_set_info.additional_properties = d
        return graph_command_graph_set_info

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
