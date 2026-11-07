from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast
import datetime






T = TypeVar("T", bound="DocumentRecovery")



@_attrs_define
class DocumentRecovery:
    """ Whether a document has work autosaved after its last save.

        Attributes:
            saved (datetime.datetime | None | Unset): When it was kept, or null when there is nothing to recover.
     """

    saved: datetime.datetime | None | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        saved: None | str | Unset
        if isinstance(self.saved, Unset):
            saved = UNSET
        elif isinstance(self.saved, datetime.datetime):
            saved = self.saved.isoformat()
        else:
            saved = self.saved


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
        })
        if saved is not UNSET:
            field_dict["saved"] = saved

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        def _parse_saved(data: object) -> datetime.datetime | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, str):
                    raise TypeError()
                saved_type_1 = datetime.datetime.fromisoformat(data)



                return saved_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(datetime.datetime | None | Unset, data)

        saved = _parse_saved(d.pop("saved", UNSET))


        document_recovery = cls(
            saved=saved,
        )


        document_recovery.additional_properties = d
        return document_recovery

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
