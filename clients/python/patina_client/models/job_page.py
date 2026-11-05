from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast

if TYPE_CHECKING:
  from ..models.job_state import JobState





T = TypeVar("T", bound="JobPage")



@_attrs_define
class JobPage:
    """ Every job the Server remembers, and the sequence a client can follow from.

        Attributes:
            jobs (list[JobState]): Newest first.
            sequence (int): The newest event's number.
     """

    jobs: list[JobState]
    sequence: int
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.job_state import JobState # noqa: PLC0415
        jobs = []
        for jobs_item_data in self.jobs:
            jobs_item = jobs_item_data.to_dict()
            jobs.append(jobs_item)



        sequence = self.sequence


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "jobs": jobs,
            "sequence": sequence,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.job_state import JobState # noqa: PLC0415
        d = dict(src_dict)
        jobs = []
        _jobs = d.pop("jobs")
        for jobs_item_data in (_jobs):
            jobs_item = JobState.from_dict(jobs_item_data)



            jobs.append(jobs_item)


        sequence = d.pop("sequence")

        job_page = cls(
            jobs=jobs,
            sequence=sequence,
        )


        job_page.additional_properties = d
        return job_page

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
