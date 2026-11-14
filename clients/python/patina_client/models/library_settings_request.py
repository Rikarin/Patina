from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset







T = TypeVar("T", bound="LibrarySettingsRequest")



@_attrs_define
class LibrarySettingsRequest:
    """ The body of `PUT /api/v1/library/settings` (#192).

        Attributes:
            name (str): The library's display name, 1 to 200 characters. Its folder keeps its name.
            texel_density (float): The texel density new projects start from, in texels per metre, above zero.
     """

    name: str
    texel_density: float
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        name = self.name

        texel_density = self.texel_density


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "name": name,
            "texelDensity": texel_density,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        name = d.pop("name")

        texel_density = d.pop("texelDensity")

        library_settings_request = cls(
            name=name,
            texel_density=texel_density,
        )


        library_settings_request.additional_properties = d
        return library_settings_request

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
