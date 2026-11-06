from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast






T = TypeVar("T", bound="StackFilterKind")



@_attrs_define
class StackFilterKind:
    """ A filter a layer can be, and the numbers it takes.

        Attributes:
            kind (str): Levels, Hsl, Blur, Invert or Grayscale.
            settings (list[str]): Its numbers' names.
     """

    kind: str
    settings: list[str]
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        kind = self.kind

        settings = self.settings




        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "kind": kind,
            "settings": settings,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        kind = d.pop("kind")

        settings = cast(list[str], d.pop("settings"))


        stack_filter_kind = cls(
            kind=kind,
            settings=settings,
        )


        stack_filter_kind.additional_properties = d
        return stack_filter_kind

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
