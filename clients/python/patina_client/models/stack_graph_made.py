from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast






T = TypeVar("T", bound="StackGraphMade")



@_attrs_define
class StackGraphMade:
    """ What writing a stack's graph made.

        Attributes:
            path (str): The graph document, below the library, as a project lists it.
            name (str): The name it was given, with a number when the one asked for was taken.
            problems (list[str]): What the stack had to say as it became a graph, each said once, such as a layer left out
                because it has
                nothing to show: the graph has the rest.
     """

    path: str
    name: str
    problems: list[str]
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        path = self.path

        name = self.name

        problems = self.problems




        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "path": path,
            "name": name,
            "problems": problems,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        path = d.pop("path")

        name = d.pop("name")

        problems = cast(list[str], d.pop("problems"))


        stack_graph_made = cls(
            path=path,
            name=name,
            problems=problems,
        )


        stack_graph_made.additional_properties = d
        return stack_graph_made

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
