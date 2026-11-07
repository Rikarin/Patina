from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast






T = TypeVar("T", bound="StackMeshParts")



@_attrs_define
class StackMeshParts:
    """ The named meshes of a stack's model, and the picture of some of them.

        Attributes:
            parts (list[str]): The named meshes in its texture set.
            asset (None | str | Unset): The picture, when one was asked for.
     """

    parts: list[str]
    asset: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        parts = self.parts



        asset: None | str | Unset
        if isinstance(self.asset, Unset):
            asset = UNSET
        else:
            asset = self.asset


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "parts": parts,
        })
        if asset is not UNSET:
            field_dict["asset"] = asset

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        parts = cast(list[str], d.pop("parts"))


        def _parse_asset(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        asset = _parse_asset(d.pop("asset", UNSET))


        stack_mesh_parts = cls(
            parts=parts,
            asset=asset,
        )


        stack_mesh_parts.additional_properties = d
        return stack_mesh_parts

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
