from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast
from uuid import UUID






T = TypeVar("T", bound="ProjectDocumentTagsRequest")



@_attrs_define
class ProjectDocumentTagsRequest:
    """ The body of `PATCH /api/v1/library/project/document/tags`.

        Attributes:
            project (str): The project, below `projects/`.
            document (UUID): The document.
            tags (list[str]): Every tag it has from now on. Repeats and empty ones are dropped.
     """

    project: str
    document: UUID
    tags: list[str]
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        project = self.project

        document = str(self.document)

        tags = self.tags




        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "project": project,
            "document": document,
            "tags": tags,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        project = d.pop("project")

        document = UUID(d.pop("document"))




        tags = cast(list[str], d.pop("tags"))


        project_document_tags_request = cls(
            project=project,
            document=document,
            tags=tags,
        )


        project_document_tags_request.additional_properties = d
        return project_document_tags_request

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
