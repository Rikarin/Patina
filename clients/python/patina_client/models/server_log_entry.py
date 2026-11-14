from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast
import datetime






T = TypeVar("T", bound="ServerLogEntry")



@_attrs_define
class ServerLogEntry:
    """ One line of what the Server said, as `GET /api/v1/log` gives it (#192).

        Attributes:
            sequence (int): Its number, rising by one from the Server's start.
            at (datetime.datetime): When it was said.
            level (str): `information`, `warning`, `error` or `critical`.
            source (str): The part of the Server that said it, such as `Patina.Server.Autosave`.
            text (str): What it said, and the exception's message after it when there was one.
     """

    sequence: int
    at: datetime.datetime
    level: str
    source: str
    text: str
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        sequence = self.sequence

        at = self.at.isoformat()

        level = self.level

        source = self.source

        text = self.text


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "sequence": sequence,
            "at": at,
            "level": level,
            "source": source,
            "text": text,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        sequence = d.pop("sequence")

        at = datetime.datetime.fromisoformat(d.pop("at"))




        level = d.pop("level")

        source = d.pop("source")

        text = d.pop("text")

        server_log_entry = cls(
            sequence=sequence,
            at=at,
            level=level,
            source=source,
            text=text,
        )


        server_log_entry.additional_properties = d
        return server_log_entry

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
