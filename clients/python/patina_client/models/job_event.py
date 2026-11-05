from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast

if TYPE_CHECKING:
  from ..models.job_state import JobState





T = TypeVar("T", bound="JobEvent")



@_attrs_define
class JobEvent:
    """ A job changing. Numbered, so a client that dropped its socket can ask what it missed.

        Attributes:
            sequence (int): The number, rising with each event.
            job (JobState): A piece of work the Server owns, which outlives the Worker running it and the Server itself.
     """

    sequence: int
    job: JobState
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.job_state import JobState # noqa: PLC0415
        sequence = self.sequence

        job = self.job.to_dict()


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "sequence": sequence,
            "job": job,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.job_state import JobState # noqa: PLC0415
        d = dict(src_dict)
        sequence = d.pop("sequence")

        job = JobState.from_dict(d.pop("job"))




        job_event = cls(
            sequence=sequence,
            job=job,
        )


        job_event.additional_properties = d
        return job_event

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
