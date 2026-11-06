from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from uuid import UUID






T = TypeVar("T", bound="ProjectDocumentRoleRequest")



@_attrs_define
class ProjectDocumentRoleRequest:
    """ The body of `PATCH /api/v1/library/project/document/role`.

        Attributes:
            project (str): The project, below `projects/`.
            document (UUID): The graph.
            role (str): `material`, `mask`, `brush` or `filter`.
     """

    project: str
    document: UUID
    role: str
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        project = self.project

        document = str(self.document)

        role = self.role


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "project": project,
            "document": document,
            "role": role,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        project = d.pop("project")

        document = UUID(d.pop("document"))




        role = d.pop("role")

        project_document_role_request = cls(
            project=project,
            document=document,
            role=role,
        )


        project_document_role_request.additional_properties = d
        return project_document_role_request

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
