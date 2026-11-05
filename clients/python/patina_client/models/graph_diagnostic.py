from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast






T = TypeVar("T", bound="GraphDiagnostic")



@_attrs_define
class GraphDiagnostic:
    """ Something a graph's compiler or its file's reader has to say.

        Attributes:
            id (str): A stable code, such as `TG0005`.
            message (str): What is wrong.
            severity (str): Warning or Error.
            node (int | None | Unset): The node it concerns, or null.
            port (None | str | Unset): The port it concerns, or null.
     """

    id: str
    message: str
    severity: str
    node: int | None | Unset = UNSET
    port: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        id = self.id

        message = self.message

        severity = self.severity

        node: int | None | Unset
        if isinstance(self.node, Unset):
            node = UNSET
        else:
            node = self.node

        port: None | str | Unset
        if isinstance(self.port, Unset):
            port = UNSET
        else:
            port = self.port


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "id": id,
            "message": message,
            "severity": severity,
        })
        if node is not UNSET:
            field_dict["node"] = node
        if port is not UNSET:
            field_dict["port"] = port

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        id = d.pop("id")

        message = d.pop("message")

        severity = d.pop("severity")

        def _parse_node(data: object) -> int | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(int | None | Unset, data)

        node = _parse_node(d.pop("node", UNSET))


        def _parse_port(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        port = _parse_port(d.pop("port", UNSET))


        graph_diagnostic = cls(
            id=id,
            message=message,
            severity=severity,
            node=node,
            port=port,
        )


        graph_diagnostic.additional_properties = d
        return graph_diagnostic

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
