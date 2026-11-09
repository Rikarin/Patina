from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset







T = TypeVar("T", bound="BakeColourOptions")



@_attrs_define
class BakeColourOptions:
    """ Where a colour map's colour comes from.

        Attributes:
            source (str): `auto`: the high mesh's vertex colours, else its materials. `vertex-colour`; `material`, each
                            material's base colour and base colour picture; or `texture`, the picture at string
                BakeColourOptions.Texture
                            read at the high mesh's own coordinates, which moves any map from one UV layout to another.
            texture (str): For `texture`: the picture to carry across, as a path on the Server's machine.
     """

    source: str
    texture: str
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        source = self.source

        texture = self.texture


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "source": source,
            "texture": texture,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        source = d.pop("source")

        texture = d.pop("texture")

        bake_colour_options = cls(
            source=source,
            texture=texture,
        )


        bake_colour_options.additional_properties = d
        return bake_colour_options

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
