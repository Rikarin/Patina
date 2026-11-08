from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast
import datetime






T = TypeVar("T", bound="LibraryReport")



@_attrs_define
class LibraryReport:
    """ What a whole build did.

        Attributes:
            build (str): The job, or a name a headless build gave itself.
            started (datetime.datetime): When it started, in UTC.
            projects (int): Projects something happened to.
            built (int): Outputs that ran.
            reused (int): Outputs whose result existed.
            fresh (int): Outputs nothing touched.
            failed (int): Outputs and exports that could not be made.
            exported (int): Exports written.
            exports_fresh (int): Exports nothing touched.
            files (int): Files written by exports.
            folder (str): The folder holding this report and one file per project.
            finished (datetime.datetime | None | Unset): When it finished, while it has not null.
            nothing (None | str | Unset): Why a finished build wrote nothing, or null when it wrote something or something
                failed.
            current (None | str | Unset): What a running build is on, for a person: a project and an output or a preset.
                Null once it ends.
     """

    build: str
    started: datetime.datetime
    projects: int
    built: int
    reused: int
    fresh: int
    failed: int
    exported: int
    exports_fresh: int
    files: int
    folder: str
    finished: datetime.datetime | None | Unset = UNSET
    nothing: None | str | Unset = UNSET
    current: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        build = self.build

        started = self.started.isoformat()

        projects = self.projects

        built = self.built

        reused = self.reused

        fresh = self.fresh

        failed = self.failed

        exported = self.exported

        exports_fresh = self.exports_fresh

        files = self.files

        folder = self.folder

        finished: None | str | Unset
        if isinstance(self.finished, Unset):
            finished = UNSET
        elif isinstance(self.finished, datetime.datetime):
            finished = self.finished.isoformat()
        else:
            finished = self.finished

        nothing: None | str | Unset
        if isinstance(self.nothing, Unset):
            nothing = UNSET
        else:
            nothing = self.nothing

        current: None | str | Unset
        if isinstance(self.current, Unset):
            current = UNSET
        else:
            current = self.current


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "build": build,
            "started": started,
            "projects": projects,
            "built": built,
            "reused": reused,
            "fresh": fresh,
            "failed": failed,
            "exported": exported,
            "exportsFresh": exports_fresh,
            "files": files,
            "folder": folder,
        })
        if finished is not UNSET:
            field_dict["finished"] = finished
        if nothing is not UNSET:
            field_dict["nothing"] = nothing
        if current is not UNSET:
            field_dict["current"] = current

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        build = d.pop("build")

        started = datetime.datetime.fromisoformat(d.pop("started"))




        projects = d.pop("projects")

        built = d.pop("built")

        reused = d.pop("reused")

        fresh = d.pop("fresh")

        failed = d.pop("failed")

        exported = d.pop("exported")

        exports_fresh = d.pop("exportsFresh")

        files = d.pop("files")

        folder = d.pop("folder")

        def _parse_finished(data: object) -> datetime.datetime | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, str):
                    raise TypeError()
                finished_type_1 = datetime.datetime.fromisoformat(data)



                return finished_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(datetime.datetime | None | Unset, data)

        finished = _parse_finished(d.pop("finished", UNSET))


        def _parse_nothing(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        nothing = _parse_nothing(d.pop("nothing", UNSET))


        def _parse_current(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        current = _parse_current(d.pop("current", UNSET))


        library_report = cls(
            build=build,
            started=started,
            projects=projects,
            built=built,
            reused=reused,
            fresh=fresh,
            failed=failed,
            exported=exported,
            exports_fresh=exports_fresh,
            files=files,
            folder=folder,
            finished=finished,
            nothing=nothing,
            current=current,
        )


        library_report.additional_properties = d
        return library_report

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
