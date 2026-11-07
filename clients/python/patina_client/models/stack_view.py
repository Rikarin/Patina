from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast






T = TypeVar("T", bound="StackView")



@_attrs_define
class StackView:
    """ A camera in the mesh's own space, which a stroke keeps because a position on a picture means nothing without
    it. Any client with a camera can fill this in; `POST /viewport/pick` gives the viewport's.

        Attributes:
            eye (list[float]): Where it is: three numbers.
            right (list[float]): The unit direction a column grows in: three numbers.
            up (list[float]): The unit direction a row shrinks in: three numbers.
            forward (list[float]): The unit direction it looks: three numbers.
            vertical_field_of_view (float): In radians.
            width (int): The picture's width, in the units stamps and the brush's radius are in.
            height (int): Its height.
     """

    eye: list[float]
    right: list[float]
    up: list[float]
    forward: list[float]
    vertical_field_of_view: float
    width: int
    height: int
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        eye = self.eye



        right = self.right



        up = self.up



        forward = self.forward



        vertical_field_of_view = self.vertical_field_of_view

        width = self.width

        height = self.height


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "eye": eye,
            "right": right,
            "up": up,
            "forward": forward,
            "verticalFieldOfView": vertical_field_of_view,
            "width": width,
            "height": height,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        eye = cast(list[float], d.pop("eye"))


        right = cast(list[float], d.pop("right"))


        up = cast(list[float], d.pop("up"))


        forward = cast(list[float], d.pop("forward"))


        vertical_field_of_view = d.pop("verticalFieldOfView")

        width = d.pop("width")

        height = d.pop("height")

        stack_view = cls(
            eye=eye,
            right=right,
            up=up,
            forward=forward,
            vertical_field_of_view=vertical_field_of_view,
            width=width,
            height=height,
        )


        stack_view.additional_properties = d
        return stack_view

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
