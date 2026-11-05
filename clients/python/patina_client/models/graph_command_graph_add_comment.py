from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..models.graph_command_graph_add_comment_op import GraphCommandGraphAddCommentOp
from ..types import UNSET, Unset
from typing import cast






T = TypeVar("T", bound="GraphCommandGraphAddComment")



@_attrs_define
class GraphCommandGraphAddComment:
    """ Adds a note.

        Attributes:
            op (GraphCommandGraphAddCommentOp):
            text (str): What it says.
            x (float): Its left edge.
            y (float): Its top edge.
            width (float | None | Unset): Its width, or null for the standard size.
            height (float | None | Unset): Its height, or null for the standard size.
            ref (int | None | Unset): A negative number the client picks. The reply says which identity it became.
     """

    op: GraphCommandGraphAddCommentOp
    text: str
    x: float
    y: float
    width: float | None | Unset = UNSET
    height: float | None | Unset = UNSET
    ref: int | None | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        op = self.op.value

        text = self.text

        x = self.x

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

        ref: int | None | Unset
        if isinstance(self.ref, Unset):
            ref = UNSET
        else:
            ref = self.ref


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "op": op,
            "text": text,
            "x": x,
            "y": y,
        })
        if width is not UNSET:
            field_dict["width"] = width
        if height is not UNSET:
            field_dict["height"] = height
        if ref is not UNSET:
            field_dict["ref"] = ref

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        op = GraphCommandGraphAddCommentOp(d.pop("op"))




        text = d.pop("text")

        x = d.pop("x")

        y = d.pop("y")

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


        def _parse_ref(data: object) -> int | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(int | None | Unset, data)

        ref = _parse_ref(d.pop("ref", UNSET))


        graph_command_graph_add_comment = cls(
            op=op,
            text=text,
            x=x,
            y=y,
            width=width,
            height=height,
            ref=ref,
        )


        graph_command_graph_add_comment.additional_properties = d
        return graph_command_graph_add_comment

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
