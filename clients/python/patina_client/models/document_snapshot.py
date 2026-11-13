from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast
from uuid import UUID
import datetime






T = TypeVar("T", bound="DocumentSnapshot")



@_attrs_define
class DocumentSnapshot:
    """ A named checkpoint of a document: its file and the paint it names, as they were when it was taken (#209).

        Attributes:
            id (UUID): Which snapshot, for a restore or a delete.
            name (str): What the person called it.
            taken (datetime.datetime): When it was taken.
            files (int): How many files it holds: the document and its paint.
            bytes_ (int): How large those files are together, before any is shared with another snapshot.
     """

    id: UUID
    name: str
    taken: datetime.datetime
    files: int
    bytes_: int
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        id = str(self.id)

        name = self.name

        taken = self.taken.isoformat()

        files = self.files

        bytes_ = self.bytes_


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "id": id,
            "name": name,
            "taken": taken,
            "files": files,
            "bytes": bytes_,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        id = UUID(d.pop("id"))




        name = d.pop("name")

        taken = datetime.datetime.fromisoformat(d.pop("taken"))




        files = d.pop("files")

        bytes_ = d.pop("bytes")

        document_snapshot = cls(
            id=id,
            name=name,
            taken=taken,
            files=files,
            bytes_=bytes_,
        )


        document_snapshot.additional_properties = d
        return document_snapshot

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
