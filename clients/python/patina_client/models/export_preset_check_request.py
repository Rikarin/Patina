from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast

if TYPE_CHECKING:
  from ..models.export_preset_document import ExportPresetDocument





T = TypeVar("T", bound="ExportPresetCheckRequest")



@_attrs_define
class ExportPresetCheckRequest:
    """ The body of `POST /api/v1/export/presets/check`: a preset to try without keeping it.

        Attributes:
            preset (ExportPresetDocument): A preset as its file says it, which is what the preset editor reads and writes
                (ADR 0019).
            project (None | str | Unset): A project whose names the file list uses, below `projects/`, or null.
     """

    preset: ExportPresetDocument
    project: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.export_preset_document import ExportPresetDocument # noqa: PLC0415
        preset = self.preset.to_dict()

        project: None | str | Unset
        if isinstance(self.project, Unset):
            project = UNSET
        else:
            project = self.project


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "preset": preset,
        })
        if project is not UNSET:
            field_dict["project"] = project

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.export_preset_document import ExportPresetDocument # noqa: PLC0415
        d = dict(src_dict)
        preset = ExportPresetDocument.from_dict(d.pop("preset"))




        def _parse_project(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        project = _parse_project(d.pop("project", UNSET))


        export_preset_check_request = cls(
            preset=preset,
            project=project,
        )


        export_preset_check_request.additional_properties = d
        return export_preset_check_request

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
