from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset







T = TypeVar("T", bound="GraphSampleRequest")



@_attrs_define
class GraphSampleRequest:
    """ The body of `POST /api/v1/graphs/{id}/sample`: a place on one node's picture (#184).

        Attributes:
            node (int): The node.
            port (str): Its output port, or the usage of an Output node.
            u (float): Across the picture, 0 at its left edge and 1 at its right.
            v (float): Down it, 0 at its top and 1 at its bottom.
     """

    node: int
    port: str
    u: float
    v: float
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        node = self.node

        port = self.port

        u = self.u

        v = self.v


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "node": node,
            "port": port,
            "u": u,
            "v": v,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        node = d.pop("node")

        port = d.pop("port")

        u = d.pop("u")

        v = d.pop("v")

        graph_sample_request = cls(
            node=node,
            port=port,
            u=u,
            v=v,
        )


        graph_sample_request.additional_properties = d
        return graph_sample_request

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
