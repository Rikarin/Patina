from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast

if TYPE_CHECKING:
  from ..models.stack_symmetry import StackSymmetry





T = TypeVar("T", bound="ViewportSymmetry")



@_attrs_define
class ViewportSymmetry:
    """ The symmetry a brush paints with, which the viewport draws where its mirror's plane meets the model (#132): the
    line the plane cuts across the surface, hidden where the model hides it. A radial symmetry draws nothing here.

        Attributes:
            symmetry (None | StackSymmetry | Unset):
     """

    symmetry: None | StackSymmetry | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.stack_symmetry import StackSymmetry # noqa: PLC0415
        symmetry: dict[str, Any] | None | Unset
        if isinstance(self.symmetry, Unset):
            symmetry = UNSET
        elif isinstance(self.symmetry, StackSymmetry):
            symmetry = self.symmetry.to_dict()
        else:
            symmetry = self.symmetry


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
        })
        if symmetry is not UNSET:
            field_dict["symmetry"] = symmetry

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.stack_symmetry import StackSymmetry # noqa: PLC0415
        d = dict(src_dict)
        def _parse_symmetry(data: object) -> None | StackSymmetry | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                symmetry_type_1 = StackSymmetry.from_dict(data)



                return symmetry_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(None | StackSymmetry | Unset, data)

        symmetry = _parse_symmetry(d.pop("symmetry", UNSET))


        viewport_symmetry = cls(
            symmetry=symmetry,
        )


        viewport_symmetry.additional_properties = d
        return viewport_symmetry

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
