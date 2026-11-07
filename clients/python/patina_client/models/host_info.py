from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset







T = TypeVar("T", bound="HostInfo")



@_attrs_define
class HostInfo:
    """ The machine and runtime a process runs on.

        Attributes:
            os (str): The operating system's description.
            architecture (str): The process architecture, such as `Arm64`.
            runtime (str): The .NET runtime's description.
     """

    os: str
    architecture: str
    runtime: str
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        os = self.os

        architecture = self.architecture

        runtime = self.runtime


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "os": os,
            "architecture": architecture,
            "runtime": runtime,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        os = d.pop("os")

        architecture = d.pop("architecture")

        runtime = d.pop("runtime")

        host_info = cls(
            os=os,
            architecture=architecture,
            runtime=runtime,
        )


        host_info.additional_properties = d
        return host_info

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
