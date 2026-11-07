from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast






T = TypeVar("T", bound="LibraryMoveRequest")



@_attrs_define
class LibraryMoveRequest:
    """ The body of `POST /api/v1/library/move`.

        Attributes:
            paths (list[str]): Folders and projects to move, below `projects/`.
            into (None | str | Unset): The folder to put them in. Null is the top.
     """

    paths: list[str]
    into: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        paths = self.paths



        into: None | str | Unset
        if isinstance(self.into, Unset):
            into = UNSET
        else:
            into = self.into


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "paths": paths,
        })
        if into is not UNSET:
            field_dict["into"] = into

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        paths = cast(list[str], d.pop("paths"))


        def _parse_into(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        into = _parse_into(d.pop("into", UNSET))


        library_move_request = cls(
            paths=paths,
            into=into,
        )


        library_move_request.additional_properties = d
        return library_move_request

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
