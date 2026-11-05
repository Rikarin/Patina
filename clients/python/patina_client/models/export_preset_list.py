from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast

if TYPE_CHECKING:
  from ..models.export_preset_info import ExportPresetInfo





T = TypeVar("T", bound="ExportPresetList")



@_attrs_define
class ExportPresetList:
    """ Every preset a library can use, and the files in its folder that are not presets.

        Attributes:
            presets (list[ExportPresetInfo]):
            problems (list[str]):
     """

    presets: list[ExportPresetInfo]
    problems: list[str]
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.export_preset_info import ExportPresetInfo # noqa: PLC0415
        presets = []
        for presets_item_data in self.presets:
            presets_item = presets_item_data.to_dict()
            presets.append(presets_item)



        problems = self.problems




        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "presets": presets,
            "problems": problems,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.export_preset_info import ExportPresetInfo # noqa: PLC0415
        d = dict(src_dict)
        presets = []
        _presets = d.pop("presets")
        for presets_item_data in (_presets):
            presets_item = ExportPresetInfo.from_dict(presets_item_data)



            presets.append(presets_item)


        problems = cast(list[str], d.pop("problems"))


        export_preset_list = cls(
            presets=presets,
            problems=problems,
        )


        export_preset_list.additional_properties = d
        return export_preset_list

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
