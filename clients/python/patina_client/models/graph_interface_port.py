from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast






T = TypeVar("T", bound="GraphInterfacePort")



@_attrs_define
class GraphInterfacePort:
    """ One port a graph has when another graph holds it as a node.

        Attributes:
            name (str): What an edge in the holding graph calls it.
            direction (str): Input or Output, as the holding graph sees it.
            kind (str): What it carries: Image, Float, Float4 and so on.
            default (list[float]): What an unwired input takes, one number per lane, or empty for the kind's zero.
            summary (str): One line about it.
     """

    name: str
    direction: str
    kind: str
    default: list[float]
    summary: str
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        name = self.name

        direction = self.direction

        kind = self.kind

        default = self.default



        summary = self.summary


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "name": name,
            "direction": direction,
            "kind": kind,
            "default": default,
            "summary": summary,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        name = d.pop("name")

        direction = d.pop("direction")

        kind = d.pop("kind")

        default = cast(list[float], d.pop("default"))


        summary = d.pop("summary")

        graph_interface_port = cls(
            name=name,
            direction=direction,
            kind=kind,
            default=default,
            summary=summary,
        )


        graph_interface_port.additional_properties = d
        return graph_interface_port

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
