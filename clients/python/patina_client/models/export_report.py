from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast






T = TypeVar("T", bound="ExportReport")



@_attrs_define
class ExportReport:
    """ One preset in a project's report.

        Attributes:
            preset (str): The preset.
            status (str): One of BuildStatuses.
            files (list[str]): Its files, absolute.
            skipped (list[str]): What it left out, and why.
            warnings (list[str]): What needs a person.
            digests (list[str]): Each of IReadOnlyList&lt;string&gt; ExportReport.Files' SHA-256, in the same order, as the
                build left it, so that two builds can be
                compared after the second has written over the first. Empty in a report from before builds kept them.
            folder (None | str | Unset): Where its files are.
            error (None | str | Unset): Why it failed.
     """

    preset: str
    status: str
    files: list[str]
    skipped: list[str]
    warnings: list[str]
    digests: list[str]
    folder: None | str | Unset = UNSET
    error: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        preset = self.preset

        status = self.status

        files = self.files



        skipped = self.skipped



        warnings = self.warnings



        digests = self.digests



        folder: None | str | Unset
        if isinstance(self.folder, Unset):
            folder = UNSET
        else:
            folder = self.folder

        error: None | str | Unset
        if isinstance(self.error, Unset):
            error = UNSET
        else:
            error = self.error


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "preset": preset,
            "status": status,
            "files": files,
            "skipped": skipped,
            "warnings": warnings,
            "digests": digests,
        })
        if folder is not UNSET:
            field_dict["folder"] = folder
        if error is not UNSET:
            field_dict["error"] = error

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        preset = d.pop("preset")

        status = d.pop("status")

        files = cast(list[str], d.pop("files"))


        skipped = cast(list[str], d.pop("skipped"))


        warnings = cast(list[str], d.pop("warnings"))


        digests = cast(list[str], d.pop("digests"))


        def _parse_folder(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        folder = _parse_folder(d.pop("folder", UNSET))


        def _parse_error(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        error = _parse_error(d.pop("error", UNSET))


        export_report = cls(
            preset=preset,
            status=status,
            files=files,
            skipped=skipped,
            warnings=warnings,
            digests=digests,
            folder=folder,
            error=error,
        )


        export_report.additional_properties = d
        return export_report

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
