from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast
import datetime

if TYPE_CHECKING:
  from ..models.bake_request import BakeRequest
  from ..models.bake_result import BakeResult
  from ..models.build_request import BuildRequest
  from ..models.library_report import LibraryReport





T = TypeVar("T", bound="JobState")



@_attrs_define
class JobState:
    """ A piece of work the Server owns, which outlives the Worker running it and the Server itself.

        Attributes:
            id (str): Its identity.
            key (str): The build key of what it makes.
            state (str): One of JobStates.
            progress (float): How far through it is, 0 to 1.
            attempts (int): How many Workers have started it.
            created (datetime.datetime): When it was asked for, in UTC.
            cancel_requested (bool): Whether a client has asked for it to stop.
            bake (BakeRequest | None | Unset):
            build (BuildRequest | None | Unset):
            report (LibraryReport | None | Unset):
            result (BakeResult | None | Unset):
            error (None | str | Unset): Why it failed.
     """

    id: str
    key: str
    state: str
    progress: float
    attempts: int
    created: datetime.datetime
    cancel_requested: bool
    bake: BakeRequest | None | Unset = UNSET
    build: BuildRequest | None | Unset = UNSET
    report: LibraryReport | None | Unset = UNSET
    result: BakeResult | None | Unset = UNSET
    error: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.bake_request import BakeRequest # noqa: PLC0415
        from ..models.bake_result import BakeResult # noqa: PLC0415
        from ..models.build_request import BuildRequest # noqa: PLC0415
        from ..models.library_report import LibraryReport # noqa: PLC0415
        id = self.id

        key = self.key

        state = self.state

        progress = self.progress

        attempts = self.attempts

        created = self.created.isoformat()

        cancel_requested = self.cancel_requested

        bake: dict[str, Any] | None | Unset
        if isinstance(self.bake, Unset):
            bake = UNSET
        elif isinstance(self.bake, BakeRequest):
            bake = self.bake.to_dict()
        else:
            bake = self.bake

        build: dict[str, Any] | None | Unset
        if isinstance(self.build, Unset):
            build = UNSET
        elif isinstance(self.build, BuildRequest):
            build = self.build.to_dict()
        else:
            build = self.build

        report: dict[str, Any] | None | Unset
        if isinstance(self.report, Unset):
            report = UNSET
        elif isinstance(self.report, LibraryReport):
            report = self.report.to_dict()
        else:
            report = self.report

        result: dict[str, Any] | None | Unset
        if isinstance(self.result, Unset):
            result = UNSET
        elif isinstance(self.result, BakeResult):
            result = self.result.to_dict()
        else:
            result = self.result

        error: None | str | Unset
        if isinstance(self.error, Unset):
            error = UNSET
        else:
            error = self.error


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "id": id,
            "key": key,
            "state": state,
            "progress": progress,
            "attempts": attempts,
            "created": created,
            "cancelRequested": cancel_requested,
        })
        if bake is not UNSET:
            field_dict["bake"] = bake
        if build is not UNSET:
            field_dict["build"] = build
        if report is not UNSET:
            field_dict["report"] = report
        if result is not UNSET:
            field_dict["result"] = result
        if error is not UNSET:
            field_dict["error"] = error

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.bake_request import BakeRequest # noqa: PLC0415
        from ..models.bake_result import BakeResult # noqa: PLC0415
        from ..models.build_request import BuildRequest # noqa: PLC0415
        from ..models.library_report import LibraryReport # noqa: PLC0415
        d = dict(src_dict)
        id = d.pop("id")

        key = d.pop("key")

        state = d.pop("state")

        progress = d.pop("progress")

        attempts = d.pop("attempts")

        created = datetime.datetime.fromisoformat(d.pop("created"))




        cancel_requested = d.pop("cancelRequested")

        def _parse_bake(data: object) -> BakeRequest | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                bake_type_1 = BakeRequest.from_dict(data)



                return bake_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(BakeRequest | None | Unset, data)

        bake = _parse_bake(d.pop("bake", UNSET))


        def _parse_build(data: object) -> BuildRequest | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                build_type_1 = BuildRequest.from_dict(data)



                return build_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(BuildRequest | None | Unset, data)

        build = _parse_build(d.pop("build", UNSET))


        def _parse_report(data: object) -> LibraryReport | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                report_type_1 = LibraryReport.from_dict(data)



                return report_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(LibraryReport | None | Unset, data)

        report = _parse_report(d.pop("report", UNSET))


        def _parse_result(data: object) -> BakeResult | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                result_type_1 = BakeResult.from_dict(data)



                return result_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(BakeResult | None | Unset, data)

        result = _parse_result(d.pop("result", UNSET))


        def _parse_error(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        error = _parse_error(d.pop("error", UNSET))


        job_state = cls(
            id=id,
            key=key,
            state=state,
            progress=progress,
            attempts=attempts,
            created=created,
            cancel_requested=cancel_requested,
            bake=bake,
            build=build,
            report=report,
            result=result,
            error=error,
        )


        job_state.additional_properties = d
        return job_state

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
