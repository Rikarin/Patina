from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast






T = TypeVar("T", bound="UploadsClearRequest")



@_attrs_define
class UploadsClearRequest:
    """ The body of `POST /api/v1/library/uploads/clear` (#301).

        Attributes:
            held (list[str] | None | Unset): Paths on the Server's machine, or any text naming them, that the client holds
                where no file of the library
                does yet, such as an open dialog's unsaved settings. An upload any of them names counts as used.
     """

    held: list[str] | None | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        held: list[str] | None | Unset
        if isinstance(self.held, Unset):
            held = UNSET
        elif isinstance(self.held, list):
            held = self.held


        else:
            held = self.held


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
        })
        if held is not UNSET:
            field_dict["held"] = held

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        def _parse_held(data: object) -> list[str] | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, list):
                    raise TypeError()
                held_type_1 = cast(list[str], data)

                return held_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(list[str] | None | Unset, data)

        held = _parse_held(d.pop("held", UNSET))


        uploads_clear_request = cls(
            held=held,
        )


        uploads_clear_request.additional_properties = d
        return uploads_clear_request

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
