from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast






T = TypeVar("T", bound="ExportFormatInfo")



@_attrs_define
class ExportFormatInfo:
    """ A file format a preset or an export can name.

        Attributes:
            id (str): Its name in a preset.
            label (str): Its name for a person, with its depth.
            extension (str): What its files end in.
            lanes (list[int]): The lane counts it holds.
            linear (bool): Whether it holds only linear numbers, as a float or 16-bit format does here.
            container (bool): Whether it is a GPU container, the only kind a preset can block-compress.
     """

    id: str
    label: str
    extension: str
    lanes: list[int]
    linear: bool
    container: bool
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        id = self.id

        label = self.label

        extension = self.extension

        lanes = self.lanes



        linear = self.linear

        container = self.container


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "id": id,
            "label": label,
            "extension": extension,
            "lanes": lanes,
            "linear": linear,
            "container": container,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        id = d.pop("id")

        label = d.pop("label")

        extension = d.pop("extension")

        lanes = cast(list[int], d.pop("lanes"))


        linear = d.pop("linear")

        container = d.pop("container")

        export_format_info = cls(
            id=id,
            label=label,
            extension=extension,
            lanes=lanes,
            linear=linear,
            container=container,
        )


        export_format_info.additional_properties = d
        return export_format_info

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
