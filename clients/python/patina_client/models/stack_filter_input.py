from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset







T = TypeVar("T", bound="StackFilterInput")



@_attrs_define
class StackFilterInput:
    """ One image a filter reads, as Properties offers a slot for it.

        Attributes:
            port (str): Its name, which a layer's `inputs` are keyed by.
            summary (str): What the node says it reads there.
            subject (bool): Whether it is the picture the filter changes. It reads the layers beneath in the channel being
                written until the
                layer says otherwise, and on a mask it is always the mask.
            optional (bool): Whether the filter runs with nothing there.
            channel (str): The channel it is named after, which it reads beneath until the layer says otherwise, or empty.
     """

    port: str
    summary: str
    subject: bool
    optional: bool
    channel: str
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        port = self.port

        summary = self.summary

        subject = self.subject

        optional = self.optional

        channel = self.channel


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "port": port,
            "summary": summary,
            "subject": subject,
            "optional": optional,
            "channel": channel,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        port = d.pop("port")

        summary = d.pop("summary")

        subject = d.pop("subject")

        optional = d.pop("optional")

        channel = d.pop("channel")

        stack_filter_input = cls(
            port=port,
            summary=summary,
            subject=subject,
            optional=optional,
            channel=channel,
        )


        stack_filter_input.additional_properties = d
        return stack_filter_input

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
