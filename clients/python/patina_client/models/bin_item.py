from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast
import datetime






T = TypeVar("T", bound="BinItem")



@_attrs_define
class BinItem:
    """ Something in the library's bin, which can be put back or deleted for good.

        Attributes:
            id (str): The entry, which the bin routes take.
            kind (str): `project` or `document`.
            name (str): Its display name when it was deleted.
            from_ (str): Where it lived, below the library, and where restoring puts it.
            deleted (datetime.datetime): When it was deleted.
     """

    id: str
    kind: str
    name: str
    from_: str
    deleted: datetime.datetime
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        id = self.id

        kind = self.kind

        name = self.name

        from_ = self.from_

        deleted = self.deleted.isoformat()


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "id": id,
            "kind": kind,
            "name": name,
            "from": from_,
            "deleted": deleted,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        id = d.pop("id")

        kind = d.pop("kind")

        name = d.pop("name")

        from_ = d.pop("from")

        deleted = datetime.datetime.fromisoformat(d.pop("deleted"))




        bin_item = cls(
            id=id,
            kind=kind,
            name=name,
            from_=from_,
            deleted=deleted,
        )


        bin_item.additional_properties = d
        return bin_item

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
