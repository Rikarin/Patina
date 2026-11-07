from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast






T = TypeVar("T", bound="WorkerStatus")



@_attrs_define
class WorkerStatus:
    """ The Worker as the Server sees it. All but `Connected` are null while there is none.

        Attributes:
            connected (bool): Whether a Worker is connected and answering.
            version (None | str | Unset): The Worker's release version, from its hello.
            process_id (int | None | Unset): The Worker's process id, from its hello.
            uptime_seconds (float | None | Unset): How long the Worker has run, from its last health reply.
            working_set_bytes (int | None | Unset): The Worker's memory, from its last health reply.
            busy_seconds (float | None | Unset): How long its current device work has gone without progress, from its last
                health.
     """

    connected: bool
    version: None | str | Unset = UNSET
    process_id: int | None | Unset = UNSET
    uptime_seconds: float | None | Unset = UNSET
    working_set_bytes: int | None | Unset = UNSET
    busy_seconds: float | None | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        connected = self.connected

        version: None | str | Unset
        if isinstance(self.version, Unset):
            version = UNSET
        else:
            version = self.version

        process_id: int | None | Unset
        if isinstance(self.process_id, Unset):
            process_id = UNSET
        else:
            process_id = self.process_id

        uptime_seconds: float | None | Unset
        if isinstance(self.uptime_seconds, Unset):
            uptime_seconds = UNSET
        else:
            uptime_seconds = self.uptime_seconds

        working_set_bytes: int | None | Unset
        if isinstance(self.working_set_bytes, Unset):
            working_set_bytes = UNSET
        else:
            working_set_bytes = self.working_set_bytes

        busy_seconds: float | None | Unset
        if isinstance(self.busy_seconds, Unset):
            busy_seconds = UNSET
        else:
            busy_seconds = self.busy_seconds


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "connected": connected,
        })
        if version is not UNSET:
            field_dict["version"] = version
        if process_id is not UNSET:
            field_dict["processId"] = process_id
        if uptime_seconds is not UNSET:
            field_dict["uptimeSeconds"] = uptime_seconds
        if working_set_bytes is not UNSET:
            field_dict["workingSetBytes"] = working_set_bytes
        if busy_seconds is not UNSET:
            field_dict["busySeconds"] = busy_seconds

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        connected = d.pop("connected")

        def _parse_version(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        version = _parse_version(d.pop("version", UNSET))


        def _parse_process_id(data: object) -> int | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(int | None | Unset, data)

        process_id = _parse_process_id(d.pop("processId", UNSET))


        def _parse_uptime_seconds(data: object) -> float | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(float | None | Unset, data)

        uptime_seconds = _parse_uptime_seconds(d.pop("uptimeSeconds", UNSET))


        def _parse_working_set_bytes(data: object) -> int | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(int | None | Unset, data)

        working_set_bytes = _parse_working_set_bytes(d.pop("workingSetBytes", UNSET))


        def _parse_busy_seconds(data: object) -> float | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(float | None | Unset, data)

        busy_seconds = _parse_busy_seconds(d.pop("busySeconds", UNSET))


        worker_status = cls(
            connected=connected,
            version=version,
            process_id=process_id,
            uptime_seconds=uptime_seconds,
            working_set_bytes=working_set_bytes,
            busy_seconds=busy_seconds,
        )


        worker_status.additional_properties = d
        return worker_status

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
