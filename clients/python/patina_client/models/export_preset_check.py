from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast

if TYPE_CHECKING:
  from ..models.export_preset_file_name import ExportPresetFileName





T = TypeVar("T", bound="ExportPresetCheck")



@_attrs_define
class ExportPresetCheck:
    """ What a preset would do, before it is kept.

        Attributes:
            files (list[ExportPresetFileName]): Each file it would write for the project, or for a project called by its id.
            text (str): The file it would be.
            problem (None | str | Unset): Why it cannot be kept, or null when it can.
     """

    files: list[ExportPresetFileName]
    text: str
    problem: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.export_preset_file_name import ExportPresetFileName # noqa: PLC0415
        files = []
        for files_item_data in self.files:
            files_item = files_item_data.to_dict()
            files.append(files_item)



        text = self.text

        problem: None | str | Unset
        if isinstance(self.problem, Unset):
            problem = UNSET
        else:
            problem = self.problem


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "files": files,
            "text": text,
        })
        if problem is not UNSET:
            field_dict["problem"] = problem

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.export_preset_file_name import ExportPresetFileName # noqa: PLC0415
        d = dict(src_dict)
        files = []
        _files = d.pop("files")
        for files_item_data in (_files):
            files_item = ExportPresetFileName.from_dict(files_item_data)



            files.append(files_item)


        text = d.pop("text")

        def _parse_problem(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        problem = _parse_problem(d.pop("problem", UNSET))


        export_preset_check = cls(
            files=files,
            text=text,
            problem=problem,
        )


        export_preset_check.additional_properties = d
        return export_preset_check

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
