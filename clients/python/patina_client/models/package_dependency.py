from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast






T = TypeVar("T", bound="PackageDependency")



@_attrs_define
class PackageDependency:
    """ Something a graph reads that publishing it as a package has to bring, or cannot.

        Attributes:
            kind (str): `compound` (a graph of its project's, which the package carries), `shipped` (a compound that ships
                            with Patina, which it does not), `picture` (a bitmap, which it carries), or `unknown` (a node type
                            nothing publishes).
            name (str): The node type, or the picture as the graph names it.
            file (None | str | Unset): Where it was found, below the library when it is in one, or null when it was not.
            problem (None | str | Unset): Why it cannot be brought, or null when it can.
            in_ (None | str | Unset): The compound that names it, or null when the graph itself does, which is when
                relinking the graph's Bitmap
                nodes (`POST /api/v1/graphs/{id}/relink`) reaches it.
     """

    kind: str
    name: str
    file: None | str | Unset = UNSET
    problem: None | str | Unset = UNSET
    in_: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        kind = self.kind

        name = self.name

        file: None | str | Unset
        if isinstance(self.file, Unset):
            file = UNSET
        else:
            file = self.file

        problem: None | str | Unset
        if isinstance(self.problem, Unset):
            problem = UNSET
        else:
            problem = self.problem

        in_: None | str | Unset
        if isinstance(self.in_, Unset):
            in_ = UNSET
        else:
            in_ = self.in_


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "kind": kind,
            "name": name,
        })
        if file is not UNSET:
            field_dict["file"] = file
        if problem is not UNSET:
            field_dict["problem"] = problem
        if in_ is not UNSET:
            field_dict["in"] = in_

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        kind = d.pop("kind")

        name = d.pop("name")

        def _parse_file(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        file = _parse_file(d.pop("file", UNSET))


        def _parse_problem(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        problem = _parse_problem(d.pop("problem", UNSET))


        def _parse_in_(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        in_ = _parse_in_(d.pop("in", UNSET))


        package_dependency = cls(
            kind=kind,
            name=name,
            file=file,
            problem=problem,
            in_=in_,
        )


        package_dependency.additional_properties = d
        return package_dependency

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
