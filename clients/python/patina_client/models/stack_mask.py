from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast

if TYPE_CHECKING:
  from ..models.stack_mask_effect import StackMaskEffect





T = TypeVar("T", bound="StackMask")



@_attrs_define
class StackMask:
    """ What decides where a layer shows.

        Attributes:
            source (str): None, Constant, Texture, Anchor, Generator, Bake or Paint.
            value (float): A constant mask's value. Default: 1.0.
            asset (str): The image a texture mask reads. Default: ''.
            anchor (str): The layer an anchor mask reads. Default: ''.
            generator (str): The compound a generator runs, such as Generators/Dirt. Default: ''.
            map_ (str): The mesh map a bake mask reads. Default: ''.
            tolerance (float): How far from that colour still counts. Default: 0.02.
            softness (float): How much further the mask takes to reach nothing. Default: 0.0.
            colour (list[float] | None | Unset): The colour a bake mask selects from its map, as three numbers, or null for
                all of it.
            effects (list[StackMaskEffect] | None | Unset): What runs on the mask, in order, or null to keep them.
     """

    source: str
    value: float = 1.0
    asset: str = ''
    anchor: str = ''
    generator: str = ''
    map_: str = ''
    tolerance: float = 0.02
    softness: float = 0.0
    colour: list[float] | None | Unset = UNSET
    effects: list[StackMaskEffect] | None | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.stack_mask_effect import StackMaskEffect # noqa: PLC0415
        source = self.source

        value = self.value

        asset = self.asset

        anchor = self.anchor

        generator = self.generator

        map_ = self.map_

        tolerance = self.tolerance

        softness = self.softness

        colour: list[float] | None | Unset
        if isinstance(self.colour, Unset):
            colour = UNSET
        elif isinstance(self.colour, list):
            colour = self.colour


        else:
            colour = self.colour

        effects: list[dict[str, Any]] | None | Unset
        if isinstance(self.effects, Unset):
            effects = UNSET
        elif isinstance(self.effects, list):
            effects = []
            for effects_type_1_item_data in self.effects:
                effects_type_1_item = effects_type_1_item_data.to_dict()
                effects.append(effects_type_1_item)


        else:
            effects = self.effects


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "source": source,
            "value": value,
            "asset": asset,
            "anchor": anchor,
            "generator": generator,
            "map": map_,
            "tolerance": tolerance,
            "softness": softness,
        })
        if colour is not UNSET:
            field_dict["colour"] = colour
        if effects is not UNSET:
            field_dict["effects"] = effects

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.stack_mask_effect import StackMaskEffect # noqa: PLC0415
        d = dict(src_dict)
        source = d.pop("source")

        value = d.pop("value")

        asset = d.pop("asset")

        anchor = d.pop("anchor")

        generator = d.pop("generator")

        map_ = d.pop("map")

        tolerance = d.pop("tolerance")

        softness = d.pop("softness")

        def _parse_colour(data: object) -> list[float] | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, list):
                    raise TypeError()
                colour_type_1 = cast(list[float], data)

                return colour_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(list[float] | None | Unset, data)

        colour = _parse_colour(d.pop("colour", UNSET))


        def _parse_effects(data: object) -> list[StackMaskEffect] | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, list):
                    raise TypeError()
                effects_type_1 = []
                _effects_type_1 = data
                for effects_type_1_item_data in (_effects_type_1):
                    effects_type_1_item = StackMaskEffect.from_dict(effects_type_1_item_data)



                    effects_type_1.append(effects_type_1_item)

                return effects_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(list[StackMaskEffect] | None | Unset, data)

        effects = _parse_effects(d.pop("effects", UNSET))


        stack_mask = cls(
            source=source,
            value=value,
            asset=asset,
            anchor=anchor,
            generator=generator,
            map_=map_,
            tolerance=tolerance,
            softness=softness,
            colour=colour,
            effects=effects,
        )


        stack_mask.additional_properties = d
        return stack_mask

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
