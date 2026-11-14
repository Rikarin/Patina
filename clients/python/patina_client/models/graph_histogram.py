from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast






T = TypeVar("T", bound="GraphHistogram")



@_attrs_define
class GraphHistogram:
    """ A node's picture counted into a histogram per channel, on the GPU, at the size the graph computes it. Each list
    is 256 shares of the picture's texels, summing to one: bin `b` holds the values `v` with
    `floor(256 v) = b`, clamped, so a value of one shares the top bin. Numbers about the picture, not its
    pixels.

        Attributes:
            node (int): The node.
            port (str): Its output port, or the usage of an Output node.
            width (int): The picture's width as counted, which is the size the graph computes it.
            height (int): Its height.
            red (list[float]): The red channel's shares.
            green (list[float]): The green channel's.
            blue (list[float]): The blue channel's.
            alpha (list[float]): The alpha channel's.
            luma (list[float]): Rec. 709 luma's, weighted from the stored red, green and blue.
     """

    node: int
    port: str
    width: int
    height: int
    red: list[float]
    green: list[float]
    blue: list[float]
    alpha: list[float]
    luma: list[float]
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        node = self.node

        port = self.port

        width = self.width

        height = self.height

        red = self.red



        green = self.green



        blue = self.blue



        alpha = self.alpha



        luma = self.luma




        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "node": node,
            "port": port,
            "width": width,
            "height": height,
            "red": red,
            "green": green,
            "blue": blue,
            "alpha": alpha,
            "luma": luma,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        node = d.pop("node")

        port = d.pop("port")

        width = d.pop("width")

        height = d.pop("height")

        red = cast(list[float], d.pop("red"))


        green = cast(list[float], d.pop("green"))


        blue = cast(list[float], d.pop("blue"))


        alpha = cast(list[float], d.pop("alpha"))


        luma = cast(list[float], d.pop("luma"))


        graph_histogram = cls(
            node=node,
            port=port,
            width=width,
            height=height,
            red=red,
            green=green,
            blue=blue,
            alpha=alpha,
            luma=luma,
        )


        graph_histogram.additional_properties = d
        return graph_histogram

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
