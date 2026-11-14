from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast






T = TypeVar("T", bound="StackSurfaceSymmetry")



@_attrs_define
class StackSurfaceSymmetry:
    """ How a decal or a path copies itself over the model (#132): mirrored through a plane, or turned round a line,
    as a stroke's symmetry copies a stroke. The plane or the line is a place on the model, in its own units, so it
    is the same in every texture set.

        Attributes:
            axis (list[float]): The plane's normal, or the line's direction: three numbers, any length but none.
            through (list[float]): A point on the plane or the line, in the model's units: three numbers.
            kind (str): `mirror` through a plane, or `radial` round a line. Default: 'mirror'.
            count (int): How many a radial symmetry makes, the original among them: 2 to 64. Default: 2.
     """

    axis: list[float]
    through: list[float]
    kind: str = 'mirror'
    count: int = 2
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        axis = self.axis



        through = self.through



        kind = self.kind

        count = self.count


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "axis": axis,
            "through": through,
            "kind": kind,
            "count": count,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        axis = cast(list[float], d.pop("axis"))


        through = cast(list[float], d.pop("through"))


        kind = d.pop("kind")

        count = d.pop("count")

        stack_surface_symmetry = cls(
            axis=axis,
            through=through,
            kind=kind,
            count=count,
        )


        stack_surface_symmetry.additional_properties = d
        return stack_surface_symmetry

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
