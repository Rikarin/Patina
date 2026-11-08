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
  from ..models.stack_input_parameters_type_1 import StackInputParametersType1
  from ..models.stack_input_values_type_1 import StackInputValuesType1





T = TypeVar("T", bound="StackInput")



@_attrs_define
class StackInput:
    """ Where one image input of a filter, a mask step or a graph fill reads.

        Attributes:
            source (str): Beneath, Anchor, Generator, Texture or Bake.
            channel (str): The channel read beneath, by usage, or empty for the channel being written. Default: ''.
            generator (str): The node or compound read, for Generator: Source/Noise, Noise/Clouds. Default: ''.
            asset (str): The image read, for Texture. Default: ''.
            map_ (str): The mesh map read, for Bake. Default: ''.
            anchor (None | StackAnchorRef | Unset):
            values (None | StackInputValuesType1 | Unset): The generator's numbers, by port.
            parameters (None | StackInputParametersType1 | Unset): The generator's settings, by name.
     """

    source: str
    channel: str = ''
    generator: str = ''
    asset: str = ''
    map_: str = ''
    anchor: None | StackAnchorRef | Unset = UNSET
    values: None | StackInputValuesType1 | Unset = UNSET
    parameters: None | StackInputParametersType1 | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.stack_anchor_ref import StackAnchorRef # noqa: PLC0415
        from ..models.stack_input_parameters_type_1 import StackInputParametersType1 # noqa: PLC0415
        from ..models.stack_input_values_type_1 import StackInputValuesType1 # noqa: PLC0415
        source = self.source

        channel = self.channel

        generator = self.generator

        asset = self.asset

        map_ = self.map_

        anchor: dict[str, Any] | None | Unset
        if isinstance(self.anchor, Unset):
            anchor = UNSET
        elif isinstance(self.anchor, StackAnchorRef):
            anchor = self.anchor.to_dict()
        else:
            anchor = self.anchor

        values: dict[str, Any] | None | Unset
        if isinstance(self.values, Unset):
            values = UNSET
        elif isinstance(self.values, StackInputValuesType1):
            values = self.values.to_dict()
        else:
            values = self.values

        parameters: dict[str, Any] | None | Unset
        if isinstance(self.parameters, Unset):
            parameters = UNSET
        elif isinstance(self.parameters, StackInputParametersType1):
            parameters = self.parameters.to_dict()
        else:
            parameters = self.parameters


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "source": source,
            "channel": channel,
            "generator": generator,
            "asset": asset,
            "map": map_,
        })
        if anchor is not UNSET:
            field_dict["anchor"] = anchor
        if values is not UNSET:
            field_dict["values"] = values
        if parameters is not UNSET:
            field_dict["parameters"] = parameters

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.stack_anchor_ref import StackAnchorRef # noqa: PLC0415
        from ..models.stack_input_parameters_type_1 import StackInputParametersType1 # noqa: PLC0415
        from ..models.stack_input_values_type_1 import StackInputValuesType1 # noqa: PLC0415
        d = dict(src_dict)
        source = d.pop("source")

        channel = d.pop("channel")

        generator = d.pop("generator")

        asset = d.pop("asset")

        map_ = d.pop("map")

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


        def _parse_values(data: object) -> None | StackInputValuesType1 | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                values_type_1 = StackInputValuesType1.from_dict(data)



                return values_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(None | StackInputValuesType1 | Unset, data)

        values = _parse_values(d.pop("values", UNSET))


        def _parse_parameters(data: object) -> None | StackInputParametersType1 | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                parameters_type_1 = StackInputParametersType1.from_dict(data)



                return parameters_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(None | StackInputParametersType1 | Unset, data)

        parameters = _parse_parameters(d.pop("parameters", UNSET))


        stack_input = cls(
            source=source,
            channel=channel,
            generator=generator,
            asset=asset,
            map_=map_,
            anchor=anchor,
            values=values,
            parameters=parameters,
        )


        stack_input.additional_properties = d
        return stack_input

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
