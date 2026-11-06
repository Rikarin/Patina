from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast






T = TypeVar("T", bound="MaterialInfo")



@_attrs_define
class MaterialInfo:
    """ Something a layer can be filled with (ADR 0012).

        Attributes:
            name (str): What it is called.
            kind (str): `graph`, which a fill reads, or `layered-material`, which unfolds into layers.
            source (str): What names it to the routes that use it.
            role (str): `material`, or `empty` for a graph with no output yet.
            origin (str): `shipped`, `project` or `shared`.
            channels (list[str]): The maps a graph makes, by usage.
     """

    name: str
    kind: str
    source: str
    role: str
    origin: str
    channels: list[str]
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        name = self.name

        kind = self.kind

        source = self.source

        role = self.role

        origin = self.origin

        channels = self.channels




        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "name": name,
            "kind": kind,
            "source": source,
            "role": role,
            "origin": origin,
            "channels": channels,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        name = d.pop("name")

        kind = d.pop("kind")

        source = d.pop("source")

        role = d.pop("role")

        origin = d.pop("origin")

        channels = cast(list[str], d.pop("channels"))


        material_info = cls(
            name=name,
            kind=kind,
            source=source,
            role=role,
            origin=origin,
            channels=channels,
        )


        material_info.additional_properties = d
        return material_info

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
