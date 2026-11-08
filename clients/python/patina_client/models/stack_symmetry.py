from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset







T = TypeVar("T", bound="StackSymmetry")



@_attrs_define
class StackSymmetry:
    """ How a stroke on a mesh copies itself: through a plane, or round an axis. The plane or the axis passes through
    the model's centre or the mesh's own origin, so a model that is not centred on its origin still mirrors onto
    itself.

        Attributes:
            kind (str): `mirror` through a plane, or `radial` round an axis. Default: 'mirror'.
            axis (str): `x`, `y` or `z`: the plane's normal, or the axis copies turn round. Default: 'x'.
            centre (str): `model`, the centre of the model's bounds, or `origin`, the mesh's own. Default: 'model'.
            offset (float): How far a mirror's plane moves along its axis from that centre, as a share of the model's
                bounding radius, -1
                to 1. A radial symmetry turns round a line along its axis, which moving along that axis does not change.
                Default: 0.0.
            count (int): How many strokes a radial symmetry makes, the stroke among them: 2 to 64. Default: 2.
     """

    kind: str = 'mirror'
    axis: str = 'x'
    centre: str = 'model'
    offset: float = 0.0
    count: int = 2
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        kind = self.kind

        axis = self.axis

        centre = self.centre

        offset = self.offset

        count = self.count


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "kind": kind,
            "axis": axis,
            "centre": centre,
            "offset": offset,
            "count": count,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        kind = d.pop("kind")

        axis = d.pop("axis")

        centre = d.pop("centre")

        offset = d.pop("offset")

        count = d.pop("count")

        stack_symmetry = cls(
            kind=kind,
            axis=axis,
            centre=centre,
            offset=offset,
            count=count,
        )


        stack_symmetry.additional_properties = d
        return stack_symmetry

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
