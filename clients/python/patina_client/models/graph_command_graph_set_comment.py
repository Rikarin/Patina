from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..models.graph_command_graph_set_comment_op import GraphCommandGraphSetCommentOp
from ..types import UNSET, Unset
from typing import cast






T = TypeVar("T", bound="GraphCommandGraphSetComment")



@_attrs_define
class GraphCommandGraphSetComment:
    """ Changes a note: anything left null stays as it is. A note moved by hand sends this on every step.

        Attributes:
            op (GraphCommandGraphSetCommentOp):
            id (int): The note.
            text (None | str | Unset): What it says.
            x (float | None | Unset): Its left edge.
            y (float | None | Unset): Its top edge.
            width (float | None | Unset): Its width.
            height (float | None | Unset): Its height.
     """

    op: GraphCommandGraphSetCommentOp
    id: int
    text: None | str | Unset = UNSET
    x: float | None | Unset = UNSET
    y: float | None | Unset = UNSET
    width: float | None | Unset = UNSET
    height: float | None | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        op = self.op.value

        id = self.id

        text: None | str | Unset
        if isinstance(self.text, Unset):
            text = UNSET
        else:
            text = self.text

        x: float | None | Unset
        if isinstance(self.x, Unset):
            x = UNSET
        else:
            x = self.x

        y: float | None | Unset
        if isinstance(self.y, Unset):
            y = UNSET
        else:
            y = self.y

        width: float | None | Unset
        if isinstance(self.width, Unset):
            width = UNSET
        else:
            width = self.width

        height: float | None | Unset
        if isinstance(self.height, Unset):
            height = UNSET
        else:
            height = self.height


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "op": op,
            "id": id,
        })
        if text is not UNSET:
            field_dict["text"] = text
        if x is not UNSET:
            field_dict["x"] = x
        if y is not UNSET:
            field_dict["y"] = y
        if width is not UNSET:
            field_dict["width"] = width
        if height is not UNSET:
            field_dict["height"] = height

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        op = GraphCommandGraphSetCommentOp(d.pop("op"))




        id = d.pop("id")

        def _parse_text(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        text = _parse_text(d.pop("text", UNSET))


        def _parse_x(data: object) -> float | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(float | None | Unset, data)

        x = _parse_x(d.pop("x", UNSET))


        def _parse_y(data: object) -> float | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(float | None | Unset, data)

        y = _parse_y(d.pop("y", UNSET))


        def _parse_width(data: object) -> float | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(float | None | Unset, data)

        width = _parse_width(d.pop("width", UNSET))


        def _parse_height(data: object) -> float | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(float | None | Unset, data)

        height = _parse_height(d.pop("height", UNSET))


        graph_command_graph_set_comment = cls(
            op=op,
            id=id,
            text=text,
            x=x,
            y=y,
            width=width,
            height=height,
        )


        graph_command_graph_set_comment.additional_properties = d
        return graph_command_graph_set_comment

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
