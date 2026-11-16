from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast






T = TypeVar("T", bound="ViewportLayerPaths")



@_attrs_define
class ViewportLayerPaths:
    """ OpenPBR's layers over the material as pictures, each a path on the Server's machine (#310). Each is read from
    red, as linear, but the sheen's colour, which is a linear colour (ADR 0050). A slot left null is no coat, no
    sheen, a white sheen or no transmission: what a material without the layer draws.

        Attributes:
            coat (None | str | Unset): How much clear coat lies over the base: OpenPBR's `coat_weight`.
            coat_roughness (None | str | Unset): The coat's own roughness.
            sheen (None | str | Unset): How much sheen lies over everything: OpenPBR's `fuzz_weight`.
            sheen_color (None | str | Unset): The sheen's colour.
            sheen_roughness (None | str | Unset): The sheen's roughness.
            transmission (None | str | Unset): How much of a dielectric base lets light through: `transmission_weight`.
     """

    coat: None | str | Unset = UNSET
    coat_roughness: None | str | Unset = UNSET
    sheen: None | str | Unset = UNSET
    sheen_color: None | str | Unset = UNSET
    sheen_roughness: None | str | Unset = UNSET
    transmission: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        coat: None | str | Unset
        if isinstance(self.coat, Unset):
            coat = UNSET
        else:
            coat = self.coat

        coat_roughness: None | str | Unset
        if isinstance(self.coat_roughness, Unset):
            coat_roughness = UNSET
        else:
            coat_roughness = self.coat_roughness

        sheen: None | str | Unset
        if isinstance(self.sheen, Unset):
            sheen = UNSET
        else:
            sheen = self.sheen

        sheen_color: None | str | Unset
        if isinstance(self.sheen_color, Unset):
            sheen_color = UNSET
        else:
            sheen_color = self.sheen_color

        sheen_roughness: None | str | Unset
        if isinstance(self.sheen_roughness, Unset):
            sheen_roughness = UNSET
        else:
            sheen_roughness = self.sheen_roughness

        transmission: None | str | Unset
        if isinstance(self.transmission, Unset):
            transmission = UNSET
        else:
            transmission = self.transmission


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
        })
        if coat is not UNSET:
            field_dict["coat"] = coat
        if coat_roughness is not UNSET:
            field_dict["coatRoughness"] = coat_roughness
        if sheen is not UNSET:
            field_dict["sheen"] = sheen
        if sheen_color is not UNSET:
            field_dict["sheenColor"] = sheen_color
        if sheen_roughness is not UNSET:
            field_dict["sheenRoughness"] = sheen_roughness
        if transmission is not UNSET:
            field_dict["transmission"] = transmission

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        def _parse_coat(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        coat = _parse_coat(d.pop("coat", UNSET))


        def _parse_coat_roughness(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        coat_roughness = _parse_coat_roughness(d.pop("coatRoughness", UNSET))


        def _parse_sheen(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        sheen = _parse_sheen(d.pop("sheen", UNSET))


        def _parse_sheen_color(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        sheen_color = _parse_sheen_color(d.pop("sheenColor", UNSET))


        def _parse_sheen_roughness(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        sheen_roughness = _parse_sheen_roughness(d.pop("sheenRoughness", UNSET))


        def _parse_transmission(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        transmission = _parse_transmission(d.pop("transmission", UNSET))


        viewport_layer_paths = cls(
            coat=coat,
            coat_roughness=coat_roughness,
            sheen=sheen,
            sheen_color=sheen_color,
            sheen_roughness=sheen_roughness,
            transmission=transmission,
        )


        viewport_layer_paths.additional_properties = d
        return viewport_layer_paths

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
