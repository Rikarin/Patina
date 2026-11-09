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





T = TypeVar("T", bound="ExportPresetWrite")



@_attrs_define
class ExportPresetWrite:
    """ The body of `PUT /api/v1/export/presets/{id}`: a preset to keep in the library's or a project's
    `export-presets` folder, which is a file there.

        Attributes:
            preset (ExportPresetDocument): A preset as its file says it, which is what the preset editor reads and writes
                (ADR 0019).
            project (None | str | Unset): The project it belongs to, below `projects/`, or null for the whole library.
            replaces (None | str | Unset): An id it takes the place of, in the same place, which renames it.
     """

    preset: ExportPresetDocument
    project: None | str | Unset = UNSET
    replaces: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.export_preset_document import ExportPresetDocument # noqa: PLC0415
        preset = self.preset.to_dict()

        project: None | str | Unset
        if isinstance(self.project, Unset):
            project = UNSET
        else:
            project = self.project

        replaces: None | str | Unset
        if isinstance(self.replaces, Unset):
            replaces = UNSET
        else:
            replaces = self.replaces


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "preset": preset,
        })
        if project is not UNSET:
            field_dict["project"] = project
        if replaces is not UNSET:
            field_dict["replaces"] = replaces

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


        def _parse_replaces(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        replaces = _parse_replaces(d.pop("replaces", UNSET))


        export_preset_write = cls(
            preset=preset,
            project=project,
            replaces=replaces,
        )


        export_preset_write.additional_properties = d
        return export_preset_write

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
