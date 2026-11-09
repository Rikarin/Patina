from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast

if TYPE_CHECKING:
  from ..models.stack_anchor_ref import StackAnchorRef
  from ..models.stack_mask_effect import StackMaskEffect
  from ..models.stack_mask_inputs_type_1 import StackMaskInputsType1
  from ..models.stack_mask_layer import StackMaskLayer
  from ..models.stack_mask_parameters_type_1 import StackMaskParametersType1





T = TypeVar("T", bound="StackMask")



@_attrs_define
class StackMask:
    """ What decides where a layer shows.

        Attributes:
            source (str): None, Constant, Texture, Anchor, Generator, Bake or Paint.
            value (float): A constant mask's value. Default: 1.0.
            asset (str): The image a texture mask reads. Default: ''.
            generator (str): The compound a generator runs, such as Generators/Dirt. Default: ''.
            map_ (str): The mesh map a bake mask reads. Default: ''.
            tolerance (float): How far from that colour still counts. Default: 0.02.
            softness (float): How much further the mask takes to reach nothing. Default: 0.0.
            lane (str): Which lane of its picture the base reads: Red, Green, Blue or Alpha. A mesh map that measures a
                direction keeps
                its X, Y and Z in the first three. Default: 'Red'.
            enabled (bool): Whether the mask applies. Switched off, the layer composites unmasked, keeping every source,
                layer and effect of
                its mask for when it is switched on again, and still keeps to the meshes it is limited to. Default: True.
            anchor (None | StackAnchorRef | Unset):
            colour (list[float] | None | Unset): The colour a bake mask selects from its map, as three numbers, or null for
                all of it.
            effects (list[StackMaskEffect] | None | Unset): What runs on the mask, in order, or null to keep them.
            parameters (None | StackMaskParametersType1 | Unset): What a generator's knobs are set to, as text, or null to
                keep them.
            mesh_parts (list[str] | None | Unset): The named meshes the layer is limited to, or null for the whole model.
            inputs (None | StackMaskInputsType1 | Unset): The image inputs of a generator that anchor points feed, by port,
                or null to keep them.
            layers (list[StackMaskLayer] | None | Unset): The mask's own stack over this base, bottom first. Ignored in a
                change: the mask layer commands edit it, so that
                a painted canvas stays with its entry.
     """

    source: str
    value: float = 1.0
    asset: str = ''
    generator: str = ''
    map_: str = ''
    tolerance: float = 0.02
    softness: float = 0.0
    lane: str = 'Red'
    enabled: bool = True
    anchor: None | StackAnchorRef | Unset = UNSET
    colour: list[float] | None | Unset = UNSET
    effects: list[StackMaskEffect] | None | Unset = UNSET
    parameters: None | StackMaskParametersType1 | Unset = UNSET
    mesh_parts: list[str] | None | Unset = UNSET
    inputs: None | StackMaskInputsType1 | Unset = UNSET
    layers: list[StackMaskLayer] | None | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.stack_anchor_ref import StackAnchorRef # noqa: PLC0415
        from ..models.stack_mask_effect import StackMaskEffect # noqa: PLC0415
        from ..models.stack_mask_inputs_type_1 import StackMaskInputsType1 # noqa: PLC0415
        from ..models.stack_mask_layer import StackMaskLayer # noqa: PLC0415
        from ..models.stack_mask_parameters_type_1 import StackMaskParametersType1 # noqa: PLC0415
        source = self.source

        value = self.value

        asset = self.asset

        generator = self.generator

        map_ = self.map_

        tolerance = self.tolerance

        softness = self.softness

        lane = self.lane

        enabled = self.enabled

        anchor: dict[str, Any] | None | Unset
        if isinstance(self.anchor, Unset):
            anchor = UNSET
        elif isinstance(self.anchor, StackAnchorRef):
            anchor = self.anchor.to_dict()
        else:
            anchor = self.anchor

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

        parameters: dict[str, Any] | None | Unset
        if isinstance(self.parameters, Unset):
            parameters = UNSET
        elif isinstance(self.parameters, StackMaskParametersType1):
            parameters = self.parameters.to_dict()
        else:
            parameters = self.parameters

        mesh_parts: list[str] | None | Unset
        if isinstance(self.mesh_parts, Unset):
            mesh_parts = UNSET
        elif isinstance(self.mesh_parts, list):
            mesh_parts = self.mesh_parts


        else:
            mesh_parts = self.mesh_parts

        inputs: dict[str, Any] | None | Unset
        if isinstance(self.inputs, Unset):
            inputs = UNSET
        elif isinstance(self.inputs, StackMaskInputsType1):
            inputs = self.inputs.to_dict()
        else:
            inputs = self.inputs

        layers: list[dict[str, Any]] | None | Unset
        if isinstance(self.layers, Unset):
            layers = UNSET
        elif isinstance(self.layers, list):
            layers = []
            for layers_type_1_item_data in self.layers:
                layers_type_1_item = layers_type_1_item_data.to_dict()
                layers.append(layers_type_1_item)


        else:
            layers = self.layers


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "source": source,
            "value": value,
            "asset": asset,
            "generator": generator,
            "map": map_,
            "tolerance": tolerance,
            "softness": softness,
            "lane": lane,
            "enabled": enabled,
        })
        if anchor is not UNSET:
            field_dict["anchor"] = anchor
        if colour is not UNSET:
            field_dict["colour"] = colour
        if effects is not UNSET:
            field_dict["effects"] = effects
        if parameters is not UNSET:
            field_dict["parameters"] = parameters
        if mesh_parts is not UNSET:
            field_dict["meshParts"] = mesh_parts
        if inputs is not UNSET:
            field_dict["inputs"] = inputs
        if layers is not UNSET:
            field_dict["layers"] = layers

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.stack_anchor_ref import StackAnchorRef # noqa: PLC0415
        from ..models.stack_mask_effect import StackMaskEffect # noqa: PLC0415
        from ..models.stack_mask_inputs_type_1 import StackMaskInputsType1 # noqa: PLC0415
        from ..models.stack_mask_layer import StackMaskLayer # noqa: PLC0415
        from ..models.stack_mask_parameters_type_1 import StackMaskParametersType1 # noqa: PLC0415
        d = dict(src_dict)
        source = d.pop("source")

        value = d.pop("value")

        asset = d.pop("asset")

        generator = d.pop("generator")

        map_ = d.pop("map")

        tolerance = d.pop("tolerance")

        softness = d.pop("softness")

        lane = d.pop("lane")

        enabled = d.pop("enabled")

        def _parse_anchor(data: object) -> None | StackAnchorRef | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                anchor_type_1 = StackAnchorRef.from_dict(data)



                return anchor_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(None | StackAnchorRef | Unset, data)

        anchor = _parse_anchor(d.pop("anchor", UNSET))


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


        def _parse_parameters(data: object) -> None | StackMaskParametersType1 | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                parameters_type_1 = StackMaskParametersType1.from_dict(data)



                return parameters_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(None | StackMaskParametersType1 | Unset, data)

        parameters = _parse_parameters(d.pop("parameters", UNSET))


        def _parse_mesh_parts(data: object) -> list[str] | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, list):
                    raise TypeError()
                mesh_parts_type_1 = cast(list[str], data)

                return mesh_parts_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(list[str] | None | Unset, data)

        mesh_parts = _parse_mesh_parts(d.pop("meshParts", UNSET))


        def _parse_inputs(data: object) -> None | StackMaskInputsType1 | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                inputs_type_1 = StackMaskInputsType1.from_dict(data)



                return inputs_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(None | StackMaskInputsType1 | Unset, data)

        inputs = _parse_inputs(d.pop("inputs", UNSET))


        def _parse_layers(data: object) -> list[StackMaskLayer] | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, list):
                    raise TypeError()
                layers_type_1 = []
                _layers_type_1 = data
                for layers_type_1_item_data in (_layers_type_1):
                    layers_type_1_item = StackMaskLayer.from_dict(layers_type_1_item_data)



                    layers_type_1.append(layers_type_1_item)

                return layers_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(list[StackMaskLayer] | None | Unset, data)

        layers = _parse_layers(d.pop("layers", UNSET))


        stack_mask = cls(
            source=source,
            value=value,
            asset=asset,
            generator=generator,
            map_=map_,
            tolerance=tolerance,
            softness=softness,
            lane=lane,
            enabled=enabled,
            anchor=anchor,
            colour=colour,
            effects=effects,
            parameters=parameters,
            mesh_parts=mesh_parts,
            inputs=inputs,
            layers=layers,
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
