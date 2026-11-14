from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast






T = TypeVar("T", bound="GraphSample")



@_attrs_define
class GraphSample:
    """ One texel of a node's picture as the graph stored it, at the size the graph computes it: its numbers at the
    precision of its format, unclamped, where a preview's PNG holds eight bits of each lane between 0 and 1.

        Attributes:
            node (int): The node.
            port (str): Its output port, or the usage of an Output node.
            x (int): The texel's column.
            y (int): Its row, from the top.
            width (int): The picture's width, which is the size the graph computes it.
            height (int): Its height.
            format_ (str): How the picture is stored: `R8`, `Rg8`, `Rgba8`, `R16Float`, `Rgba16Float`,
                `R32Float` or `Rgba32Float`.
            values (list[float]): The texel's lanes as stored: one for a grey, two for a normal's, four for a colour.
     """

    node: int
    port: str
    x: int
    y: int
    width: int
    height: int
    format_: str
    values: list[float]
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        node = self.node

        port = self.port

        x = self.x

        y = self.y

        width = self.width

        height = self.height

        format_ = self.format_

        values = self.values




        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "node": node,
            "port": port,
            "x": x,
            "y": y,
            "width": width,
            "height": height,
            "format": format_,
            "values": values,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        node = d.pop("node")

        port = d.pop("port")

        x = d.pop("x")

        y = d.pop("y")

        width = d.pop("width")

        height = d.pop("height")

        format_ = d.pop("format")

        values = cast(list[float], d.pop("values"))


        graph_sample = cls(
            node=node,
            port=port,
            x=x,
            y=y,
            width=width,
            height=height,
            format_=format_,
            values=values,
        )


        graph_sample.additional_properties = d
        return graph_sample

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
