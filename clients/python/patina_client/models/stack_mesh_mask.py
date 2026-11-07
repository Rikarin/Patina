from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast






T = TypeVar("T", bound="StackMeshMask")



@_attrs_define
class StackMeshMask:
    """ Limits a layer to some of the model's named meshes, on top of any mask it has.

        Attributes:
            parts (list[str]): The named meshes it stays on. Empty is the whole model.
            asset (str): The picture of where they lie, from `POST /stacks/{id}/mesh-mask`.
     """

    parts: list[str]
    asset: str
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        parts = self.parts



        asset = self.asset


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "parts": parts,
            "asset": asset,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        parts = cast(list[str], d.pop("parts"))


        asset = d.pop("asset")

        stack_mesh_mask = cls(
            parts=parts,
            asset=asset,
        )


        stack_mesh_mask.additional_properties = d
        return stack_mesh_mask

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
