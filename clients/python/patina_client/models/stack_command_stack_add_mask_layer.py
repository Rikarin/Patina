from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..models.stack_command_stack_add_mask_layer_op import StackCommandStackAddMaskLayerOp
from ..types import UNSET, Unset
from typing import cast






T = TypeVar("T", bound="StackCommandStackAddMaskLayer")



@_attrs_define
class StackCommandStackAddMaskLayer:
    """ Adds an entry to a layer's mask stack. A layer with no mask gets a white one under it first, so that the entry has
    something to composite over.

        Attributes:
            op (StackCommandStackAddMaskLayerOp):
            layer (str): The layer whose mask it is.
            source (str): Constant, Texture, Anchor, Generator, Bake or Paint.
            index (int | None | Unset): Where it goes, counted from the bottom of the mask's stack, or null for the top.
            generator (None | str | Unset): The compound, for a generator entry, or null for the first the vocabulary
                offers.
     """

    op: StackCommandStackAddMaskLayerOp
    layer: str
    source: str
    index: int | None | Unset = UNSET
    generator: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        op = self.op.value

        layer = self.layer

        source = self.source

        index: int | None | Unset
        if isinstance(self.index, Unset):
            index = UNSET
        else:
            index = self.index

        generator: None | str | Unset
        if isinstance(self.generator, Unset):
            generator = UNSET
        else:
            generator = self.generator


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "op": op,
            "layer": layer,
            "source": source,
        })
        if index is not UNSET:
            field_dict["index"] = index
        if generator is not UNSET:
            field_dict["generator"] = generator

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        op = StackCommandStackAddMaskLayerOp(d.pop("op"))




        layer = d.pop("layer")

        source = d.pop("source")

        def _parse_index(data: object) -> int | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(int | None | Unset, data)

        index = _parse_index(d.pop("index", UNSET))


        def _parse_generator(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        generator = _parse_generator(d.pop("generator", UNSET))


        stack_command_stack_add_mask_layer = cls(
            op=op,
            layer=layer,
            source=source,
            index=index,
            generator=generator,
        )


        stack_command_stack_add_mask_layer.additional_properties = d
        return stack_command_stack_add_mask_layer

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
