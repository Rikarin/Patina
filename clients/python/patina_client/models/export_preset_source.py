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





T = TypeVar("T", bound="ExportPresetSource")



@_attrs_define
class ExportPresetSource:
    """ A preset to edit, and where it comes from.

        Attributes:
            document (ExportPresetDocument): A preset as its file says it, which is what the preset editor reads and writes
                (ADR 0019).
            origin (str): factory, library or project.
            text (str): The file's text, as a person would read it.
            file (None | str | Unset): The file on the Server's machine, or null for a factory preset, which lives in
                Patina.
     """

    document: ExportPresetDocument
    origin: str
    text: str
    file: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.export_preset_document import ExportPresetDocument # noqa: PLC0415
        document = self.document.to_dict()

        origin = self.origin

        text = self.text

        file: None | str | Unset
        if isinstance(self.file, Unset):
            file = UNSET
        else:
            file = self.file


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "document": document,
            "origin": origin,
            "text": text,
        })
        if file is not UNSET:
            field_dict["file"] = file

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.export_preset_document import ExportPresetDocument # noqa: PLC0415
        d = dict(src_dict)
        document = ExportPresetDocument.from_dict(d.pop("document"))




        origin = d.pop("origin")

        text = d.pop("text")

        def _parse_file(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        file = _parse_file(d.pop("file", UNSET))


        export_preset_source = cls(
            document=document,
            origin=origin,
            text=text,
            file=file,
        )


        export_preset_source.additional_properties = d
        return export_preset_source

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
