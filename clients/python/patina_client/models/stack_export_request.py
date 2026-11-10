from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast






T = TypeVar("T", bound="StackExportRequest")



@_attrs_define
class StackExportRequest:
    """ Writes each channel of a stack to a file.

        Attributes:
            folder (str): Where, absolute or relative to the library.
            layer (None | str | Unset): One layer to write on its own, such as a flattened folder, composited alone over the
                channels' defaults, or
                null for the whole stack. Its files are named after it.
     """

    folder: str
    layer: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        folder = self.folder

        layer: None | str | Unset
        if isinstance(self.layer, Unset):
            layer = UNSET
        else:
            layer = self.layer


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "folder": folder,
        })
        if layer is not UNSET:
            field_dict["layer"] = layer

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        folder = d.pop("folder")

        def _parse_layer(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        layer = _parse_layer(d.pop("layer", UNSET))


        stack_export_request = cls(
            folder=folder,
            layer=layer,
        )


        stack_export_request.additional_properties = d
        return stack_export_request

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
