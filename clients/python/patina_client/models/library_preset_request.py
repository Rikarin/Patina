from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast






T = TypeVar("T", bound="LibraryPresetRequest")



@_attrs_define
class LibraryPresetRequest:
    """ The body of `PUT /api/v1/library/export/preset`.

        Attributes:
            preset (None | str | Unset): The id of the preset a project's export starts from when it has kept none, or null
                for the Starter glTF preset.
     """

    preset: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        preset: None | str | Unset
        if isinstance(self.preset, Unset):
            preset = UNSET
        else:
            preset = self.preset


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
        })
        if preset is not UNSET:
            field_dict["preset"] = preset

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        def _parse_preset(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        preset = _parse_preset(d.pop("preset", UNSET))


        library_preset_request = cls(
            preset=preset,
        )


        library_preset_request.additional_properties = d
        return library_preset_request

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
