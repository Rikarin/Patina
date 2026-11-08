from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset







T = TypeVar("T", bound="BakeIdOptions")



@_attrs_define
class BakeIdOptions:
    """ What an ID map labels, and how it colours the labels.

        Attributes:
            source (str): `auto`: vertex colours, else materials when there are several, else connected shells. Or one of
                            `material`, `material-colour` (each material's base colour), `vertex-colour`, `mesh`
                            (each named part) and `shell`.
            palette (str): How labels become colours: `random`, `hue` or `grey`. Colours stay colours.
     """

    source: str
    palette: str
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        source = self.source

        palette = self.palette


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "source": source,
            "palette": palette,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        source = d.pop("source")

        palette = d.pop("palette")

        bake_id_options = cls(
            source=source,
            palette=palette,
        )


        bake_id_options.additional_properties = d
        return bake_id_options

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
