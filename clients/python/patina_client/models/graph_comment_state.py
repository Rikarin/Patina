from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset







T = TypeVar("T", bound="GraphCommentState")



@_attrs_define
class GraphCommentState:
    """ A note on the canvas.

        Attributes:
            id (int): What a command names it by. It never changes and is never reused while a session lasts.
            text (str): What it says.
            x (float): Its left edge on the canvas.
            y (float): Its top edge on the canvas.
            width (float): Its width.
            height (float): Its height.
     """

    id: int
    text: str
    x: float
    y: float
    width: float
    height: float
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        id = self.id

        text = self.text

        x = self.x

        y = self.y

        width = self.width

        height = self.height


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "id": id,
            "text": text,
            "x": x,
            "y": y,
            "width": width,
            "height": height,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        id = d.pop("id")

        text = d.pop("text")

        x = d.pop("x")

        y = d.pop("y")

        width = d.pop("width")

        height = d.pop("height")

        graph_comment_state = cls(
            id=id,
            text=text,
            x=x,
            y=y,
            width=width,
            height=height,
        )


        graph_comment_state.additional_properties = d
        return graph_comment_state

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
