from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast

if TYPE_CHECKING:
  from ..models.project_comparison import ProjectComparison





T = TypeVar("T", bound="BuildComparison")



@_attrs_define
class BuildComparison:
    """ What differs between two builds (#217): their outputs' keys and their files' bytes, project by project.

        Attributes:
            before (str): The earlier build.
            after (str): The later build.
            projects (list[ProjectComparison]): Each project either build touched, by path, with what differs.
            changed_files (int): Files whose bytes differ or that only one build wrote.
            same_files (int): Files both builds wrote byte for byte the same.
     """

    before: str
    after: str
    projects: list[ProjectComparison]
    changed_files: int
    same_files: int
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.project_comparison import ProjectComparison # noqa: PLC0415
        before = self.before

        after = self.after

        projects = []
        for projects_item_data in self.projects:
            projects_item = projects_item_data.to_dict()
            projects.append(projects_item)



        changed_files = self.changed_files

        same_files = self.same_files


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "before": before,
            "after": after,
            "projects": projects,
            "changedFiles": changed_files,
            "sameFiles": same_files,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.project_comparison import ProjectComparison # noqa: PLC0415
        d = dict(src_dict)
        before = d.pop("before")

        after = d.pop("after")

        projects = []
        _projects = d.pop("projects")
        for projects_item_data in (_projects):
            projects_item = ProjectComparison.from_dict(projects_item_data)



            projects.append(projects_item)


        changed_files = d.pop("changedFiles")

        same_files = d.pop("sameFiles")

        build_comparison = cls(
            before=before,
            after=after,
            projects=projects,
            changed_files=changed_files,
            same_files=same_files,
        )


        build_comparison.additional_properties = d
        return build_comparison

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
