from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset







T = TypeVar("T", bound="BakeHeightOptions")



@_attrs_define
class BakeHeightOptions:
    """ The height map's stored range.

        Attributes:
            range_ (str): `measured`, the largest height found; `rays`, the longer ray distance; or `custom`,
                            double BakeHeightOptions.Scale. The last two are the same for every bake made with them, so their
                maps agree.
            scale (float): The range for `custom`, in the rays' units.
     """

    range_: str
    scale: float
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        range_ = self.range_

        scale = self.scale


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "range": range_,
            "scale": scale,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        range_ = d.pop("range")

        scale = d.pop("scale")

        bake_height_options = cls(
            range_=range_,
            scale=scale,
        )


        bake_height_options.additional_properties = d
        return bake_height_options

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
